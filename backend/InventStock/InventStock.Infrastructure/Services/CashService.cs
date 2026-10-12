using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Cash;
using InventStock.Application.Interfaces;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

public sealed class CashService(AppDbContext db, ICurrentUserService currentUser) : ICashService
{
    public async Task<IReadOnlyList<CashAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await db.CashAccounts.AsNoTracking().OrderBy(x => x.Kind).ThenBy(x => x.Currency).ToListAsync(cancellationToken);
        var openings = await db.CashOpeningBalances.AsNoTracking().ToDictionaryAsync(x => x.CashAccountId, cancellationToken);
        return accounts.Select(x => Map(x, openings.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<CashOpeningBalanceDto> SetOpeningBalanceAsync(Guid accountId, PostCashOpeningBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        var fingerprint = Fingerprint(accountId, request.CountedAmount.ToString("G29", CultureInfo.InvariantCulture), request.CountedAtUtc.ToUniversalTime().Ticks.ToString());
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);

        var replay = await db.CashOpeningBalances.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.CountedByUserId != actorId || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID is already used by another cash command.");
            await tx.CommitAsync(cancellationToken);
            return Map(replay);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);

        var account = await db.CashAccounts.SingleOrDefaultAsync(x => x.Id == accountId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Active cash account", accountId);
        if (await db.CashOpeningBalances.AnyAsync(x => x.CashAccountId == accountId, cancellationToken))
            throw new ConflictException($"An opening count already exists for {account.Name}.");

        var opening = CashOpeningBalance.Create(accountId, request.CountedAmount, request.CountedAtUtc, actorId, request.OperationId, fingerprint);
        var movement = CashMovement.Opening(accountId, request.CountedAmount, actorId, request.CountedAtUtc, opening.Id);
        opening.LinkMovement(movement.Id);
        db.CashOpeningBalances.Add(opening);
        db.CashMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(opening);
    }

    public async Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default) =>
        await db.ExpenseCategories.AsNoTracking().Where(x => includeInactive || x.IsActive).OrderBy(x => x.Name)
            .Select(x => new ExpenseCategoryDto(x.Id, x.Name, x.IsActive)).ToListAsync(cancellationToken);

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(SaveExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var normalized = request.Name.Trim().ToUpperInvariant();
        if (await db.ExpenseCategories.AnyAsync(x => x.NormalizedName == normalized, cancellationToken))
            throw new ConflictException("An expense category with this name already exists.");
        var category = ExpenseCategory.Create(request.Name);
        db.ExpenseCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return new(category.Id, category.Name, category.IsActive);
    }

    public async Task<ExpenseCategoryDto> RenameCategoryAsync(Guid id, SaveExpenseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense category", id);
        var normalized = request.Name.Trim().ToUpperInvariant();
        if (await db.ExpenseCategories.AnyAsync(x => x.Id != id && x.NormalizedName == normalized, cancellationToken))
            throw new ConflictException("An expense category with this name already exists.");
        category.Rename(request.Name);
        await db.SaveChangesAsync(cancellationToken);
        return new(category.Id, category.Name, category.IsActive);
    }

    public async Task SetCategoryActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var category = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Expense category", id);
        category.SetActive(isActive);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<OutgoingPaymentDto> PostOutgoingAsync(PostOutgoingPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        var fingerprint = PaymentFingerprint(request);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);

        var replay = await PaymentQuery().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.RecordedByUserId != actorId || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID is already used by another cash command.");
            var replayReversal = await db.OutgoingPaymentReversals.AsNoTracking().SingleOrDefaultAsync(x => x.OutgoingPaymentId == replay.Id, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Map(replay, replayReversal);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);

        var category = await db.ExpenseCategories.SingleOrDefaultAsync(x => x.Id == request.ExpenseCategoryId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Active expense category", request.ExpenseCategoryId);
        CashAccount? account = null;
        if (request.FundingSource == PaymentFundingSource.ShopAccount)
        {
            account = await db.CashAccounts.SingleOrDefaultAsync(x => x.Id == request.CashAccountId && x.IsActive, cancellationToken)
                ?? throw new NotFoundException("Active cash account", request.CashAccountId!);
            if (account.Currency != request.Currency)
                throw new ConflictException($"{account.Name} accepts {account.Currency}, not {request.Currency}.");
        }

        var payment = OutgoingPayment.Create(category.Id, request.FundingSource, account?.Id, request.OriginalAmount,
            request.Currency, request.AgreedUsdValue, request.PaidOn, request.Payee, request.Description,
            request.ForPeriod, request.ReceiptReference, actorId, request.OperationId, fingerprint);
        if (account is not null)
        {
            var occurredAt = DateTime.UtcNow;
            var movement = CashMovement.Payment(account.Id, request.OriginalAmount, actorId, occurredAt, payment.Id);
            payment.LinkMovement(movement.Id);
            db.CashMovements.Add(movement);
        }
        db.OutgoingPayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(payment, null, category, account);
    }

    public async Task<OutgoingPaymentDto> ReverseOutgoingAsync(Guid paymentId, ReverseOutgoingPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        var fingerprint = Fingerprint(paymentId, request.Reason.Trim(), request.ReversedAtUtc.ToUniversalTime().Ticks.ToString());
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);

        var replay = await db.OutgoingPaymentReversals.AsNoTracking().Include(x => x.OutgoingPayment)
            .ThenInclude(x => x.ExpenseCategory).Include(x => x.OutgoingPayment).ThenInclude(x => x.CashAccount)
            .SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.ReversedByUserId != actorId || replay.PayloadFingerprint != fingerprint || replay.OutgoingPaymentId != paymentId)
                throw new ConflictException("This operation ID is already used by another cash command.");
            await tx.CommitAsync(cancellationToken);
            return Map(replay.OutgoingPayment, replay);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);

        var payment = await PaymentQuery().SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Outgoing payment", paymentId);
        if (await db.OutgoingPaymentReversals.AnyAsync(x => x.OutgoingPaymentId == paymentId, cancellationToken))
            throw new ConflictException("This outgoing payment is already reversed.");

        var reversal = OutgoingPaymentReversal.Create(paymentId, request.Reason, request.ReversedAtUtc, actorId, request.OperationId, fingerprint);
        if (payment.CashAccountId.HasValue)
        {
            var movement = CashMovement.Reversal(payment.CashAccountId.Value, payment.OriginalAmount, actorId, request.ReversedAtUtc, reversal.Id);
            reversal.LinkMovement(movement.Id);
            db.CashMovements.Add(movement);
        }
        db.OutgoingPaymentReversals.Add(reversal);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(payment, reversal);
    }

    public async Task<OutgoingPaymentListDto> ListOutgoingsAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var payments = await PaymentQuery().Where(x => x.PaidOn >= start && x.PaidOn < end)
            .OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var ids = payments.Select(x => x.Id).ToArray();
        var reversals = await db.OutgoingPaymentReversals.AsNoTracking().Where(x => ids.Contains(x.OutgoingPaymentId))
            .ToDictionaryAsync(x => x.OutgoingPaymentId, cancellationToken);
        var dtos = payments.Select(x => Map(x, reversals.GetValueOrDefault(x.Id))).ToArray();
        var totals = payments.Where(x => !reversals.ContainsKey(x.Id)).GroupBy(x => new { x.ExpenseCategoryId, x.ExpenseCategory.Name })
            .OrderBy(x => x.Key.Name).Select(x => new MonthlyOutgoingTotalDto(x.Key.ExpenseCategoryId, x.Key.Name,
                x.Sum(y => y.AgreedUsdValue), x.Count())).ToArray();
        return new(year, month, dtos, totals);
    }

    private Guid Actor() => currentUser.UserId ?? throw new UnauthorizedAccessException();
    private IQueryable<OutgoingPayment> PaymentQuery() => db.OutgoingPayments.AsNoTracking()
        .Include(x => x.ExpenseCategory).Include(x => x.CashAccount);

    private async Task LockOperation(Guid operationId, CancellationToken cancellationToken)
    {
        await OperationGuard.LockAsync(db, operationId, "Cash", cancellationToken);
    }

    private async Task EnsureOperationUnusedElsewhere(Guid operationId, CancellationToken cancellationToken)
    {
        if (await db.CashOpeningBalances.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.OutgoingPayments.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.OutgoingPaymentReversals.AnyAsync(x => x.OperationId == operationId, cancellationToken))
            throw new ConflictException("This operation ID is already used by another cash command.");
    }

    private static string PaymentFingerprint(PostOutgoingPaymentRequest request) => Fingerprint(
        request.ExpenseCategoryId, request.FundingSource, request.CashAccountId,
        request.OriginalAmount.ToString("G29", CultureInfo.InvariantCulture), request.Currency,
        request.AgreedUsdValue.ToString("G29", CultureInfo.InvariantCulture), request.PaidOn,
        request.Payee?.Trim(), request.Description?.Trim(), request.ForPeriod?.Trim(), request.ReceiptReference?.Trim());

    private static string Fingerprint(params object?[] values) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", values.Select(x => x?.ToString() ?? "")))));

    private static CashAccountDto Map(CashAccount account, CashOpeningBalance? opening) =>
        new(account.Id, account.Code, account.Name, account.Currency, account.Kind, account.IsActive, opening is null ? null : Map(opening));
    private static CashOpeningBalanceDto Map(CashOpeningBalance opening) =>
        new(opening.Id, opening.CashAccountId, opening.CountedAmount, opening.CountedAtUtc, opening.CountedByUserId, opening.CreatedAtUtc, opening.OperationId);
    private static OutgoingPaymentDto Map(OutgoingPayment payment, OutgoingPaymentReversal? reversal,
        ExpenseCategory? category = null, CashAccount? account = null) =>
        new(payment.Id, payment.OperationId, payment.ExpenseCategoryId, (category ?? payment.ExpenseCategory).Name,
            payment.FundingSource, payment.CashAccountId, (account ?? payment.CashAccount)?.Name,
            payment.OriginalAmount, payment.Currency, payment.AgreedUsdValue, payment.PaidOn,
            payment.CreatedAtUtc, payment.RecordedByUserId, payment.Payee, payment.Description, payment.ForPeriod,
            payment.ReceiptReference, reversal is not null, reversal?.Id, reversal?.Reason, reversal?.ReversedAtUtc);
}
