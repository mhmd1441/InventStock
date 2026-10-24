using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
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

    public async Task<SupplierPaymentDto> PostSupplierPaymentAsync(PostSupplierPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        EnsureNotFutureBusinessDate(request.PaidOn);
        ValidateSupplierPaymentLines(request.Lines);
        var fingerprint = SupplierPaymentFingerprint(request);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);
        var replay = await SupplierPaymentQuery().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.RecordedByUserId != actorId || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID is already used by another cash command.");
            var reversal = await db.SupplierPaymentReversals.AsNoTracking().SingleOrDefaultAsync(x => x.SupplierPaymentId == replay.Id, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Map(replay, reversal);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Active supplier", request.SupplierId);
        var account = await db.CashAccounts.SingleOrDefaultAsync(x => x.Id == request.CashAccountId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("Active cash account", request.CashAccountId);
        if (request.Lines.Any(x => x.Currency != account.Currency))
            throw new ConflictException($"{account.Name} accepts {account.Currency}, not a different payment currency.");

        var payment = SupplierPayment.Create(supplier.Id, account.Id, request.PaidOn, request.Note,
            request.ReceiptReference, actorId, request.OperationId, fingerprint);
        var occurredAtUtc = BusinessDayMomentUtc(request.PaidOn);
        foreach (var input in request.Lines)
        {
            var line = payment.AddLine(input.Kind, input.OriginalAmount, input.Currency, input.AgreedUsdValue);
            var movement = CashMovement.SupplierPayment(line, account.Id, actorId, occurredAtUtc);
            line.LinkMovement(movement.Id);
            db.CashMovements.Add(movement);
        }
        db.SupplierPayments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(payment, null, supplier, account);
    }

    public async Task<SupplierPaymentDto> ReverseSupplierPaymentAsync(Guid paymentId, ReverseSupplierPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        var fingerprint = Fingerprint(paymentId, request.Reason.Trim(), request.ReversedAtUtc.ToUniversalTime().Ticks.ToString());
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);
        var replay = await db.SupplierPaymentReversals.AsNoTracking().Include(x => x.SupplierPayment).ThenInclude(x => x.Lines)
            .Include(x => x.SupplierPayment).ThenInclude(x => x.Supplier).Include(x => x.SupplierPayment).ThenInclude(x => x.CashAccount)
            .SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.ReversedByUserId != actorId || replay.PayloadFingerprint != fingerprint || replay.SupplierPaymentId != paymentId)
                throw new ConflictException("This operation ID is already used by another cash command.");
            await tx.CommitAsync(cancellationToken);
            return Map(replay.SupplierPayment, replay);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);
        var payment = await SupplierPaymentQuery().SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new NotFoundException("Supplier payment", paymentId);
        if (await db.SupplierPaymentReversals.AnyAsync(x => x.SupplierPaymentId == paymentId, cancellationToken))
            throw new ConflictException("This supplier payment is already reversed.");
        var reversal = SupplierPaymentReversal.Create(paymentId, request.Reason, request.ReversedAtUtc, actorId, request.OperationId, fingerprint);
        foreach (var line in payment.Lines)
            db.CashMovements.Add(CashMovement.SupplierPaymentReversal(payment.CashAccountId, line.OriginalAmount, actorId, request.ReversedAtUtc, reversal.Id));
        db.SupplierPaymentReversals.Add(reversal);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(payment, reversal);
    }

    public async Task<IReadOnlyList<SupplierPaymentDto>> ListSupplierPaymentsAsync(int year, int month,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1);
        var payments = await SupplierPaymentQuery().Where(x => x.PaidOn >= start && x.PaidOn < end)
            .OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var ids = payments.Select(x => x.Id).ToArray();
        var reversals = await db.SupplierPaymentReversals.AsNoTracking().Where(x => ids.Contains(x.SupplierPaymentId))
            .ToDictionaryAsync(x => x.SupplierPaymentId, cancellationToken);
        return payments.Select(x => Map(x, reversals.GetValueOrDefault(x.Id))).ToArray();
    }

    public async Task<CashTransferDto> PostTransferAsync(PostCashTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        EnsureNotFutureBusinessDate(request.TransferredOn);
        var fingerprint = TransferFingerprint(request);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);
        var replay = await db.CashTransfers.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.RecordedByUserId != actorId || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID is already used by another cash command.");
            var reversal = await db.CashTransferReversals.AsNoTracking().SingleOrDefaultAsync(x => x.CashTransferId == replay.Id, cancellationToken);
            var replayAccounts = await TransferAccountsAsync(replay, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Map(replay, reversal, replayAccounts);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);
        if (request.SourceCashAccountId == request.DestinationCashAccountId)
            throw new ConflictException("Source and destination accounts must differ.");
        if (request.SourceAmount <= 0 || request.DestinationAmount <= 0 || request.AgreedRate <= 0)
            throw new ValidationException("Transfer amounts and agreed rate must be positive.");
        if (request.FeeAmount.HasValue != request.FeeCashAccountId.HasValue || request.FeeAmount.HasValue != request.FeeCurrency.HasValue || request.FeeAmount is <= 0)
            throw new ValidationException("A transfer fee requires a positive amount, account and currency together.");

        var ids = new[] { request.SourceCashAccountId, request.DestinationCashAccountId, request.FeeCashAccountId }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var accounts = await db.CashAccounts.Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        if (accounts.Count != ids.Length) throw new NotFoundException("Active cash account", ids.First(x => !accounts.ContainsKey(x)));
        if (accounts[request.SourceCashAccountId].Currency != request.SourceCurrency || accounts[request.DestinationCashAccountId].Currency != request.DestinationCurrency)
            throw new ConflictException("Transfer currencies must match their selected cash accounts.");
        if (request.FeeCashAccountId.HasValue && accounts[request.FeeCashAccountId.Value].Currency != request.FeeCurrency)
            throw new ConflictException("Transfer fee currency must match its selected cash account.");

        var transfer = CashTransfer.Create(request.SourceCashAccountId, request.DestinationCashAccountId, request.FeeCashAccountId,
            request.TransferredOn, request.SourceAmount, request.SourceCurrency, request.DestinationAmount, request.DestinationCurrency,
            request.AgreedRate, request.FeeAmount, request.FeeCurrency, request.Note, actorId, request.OperationId, fingerprint);
        var occurredAtUtc = BusinessDayMomentUtc(request.TransferredOn);
        db.CashTransfers.Add(transfer);
        db.CashMovements.Add(CashMovement.Transfer(transfer.SourceCashAccountId, CashMovementType.TransferOut, -transfer.SourceAmount, actorId, occurredAtUtc, transfer.Id));
        db.CashMovements.Add(CashMovement.Transfer(transfer.DestinationCashAccountId, CashMovementType.TransferIn, transfer.DestinationAmount, actorId, occurredAtUtc, transfer.Id));
        if (transfer.FeeCashAccountId.HasValue)
            db.CashMovements.Add(CashMovement.Transfer(transfer.FeeCashAccountId.Value, CashMovementType.TransferFee, -transfer.FeeAmount!.Value, actorId, occurredAtUtc, transfer.Id));
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(transfer, null, accounts);
    }

    public async Task<CashTransferDto> ReverseTransferAsync(Guid transferId, ReverseCashTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        var fingerprint = Fingerprint(transferId, request.Reason.Trim(), request.ReversedAtUtc.ToUniversalTime().Ticks.ToString());
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);
        var replay = await db.CashTransferReversals.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.ReversedByUserId != actorId || replay.PayloadFingerprint != fingerprint || replay.CashTransferId != transferId)
                throw new ConflictException("This operation ID is already used by another cash command.");
            var replayTransfer = await db.CashTransfers.AsNoTracking().SingleAsync(x => x.Id == transferId, cancellationToken);
            var replayAccounts = await TransferAccountsAsync(replayTransfer, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Map(replayTransfer, replay, replayAccounts);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);
        var transfer = await db.CashTransfers.SingleOrDefaultAsync(x => x.Id == transferId, cancellationToken)
            ?? throw new NotFoundException("Cash transfer", transferId);
        if (await db.CashTransferReversals.AnyAsync(x => x.CashTransferId == transferId, cancellationToken))
            throw new ConflictException("This cash transfer is already reversed.");
        var reversal = CashTransferReversal.Create(transferId, request.Reason, request.ReversedAtUtc, actorId, request.OperationId, fingerprint);
        db.CashTransferReversals.Add(reversal);
        db.CashMovements.Add(CashMovement.TransferReversal(transfer.SourceCashAccountId, transfer.SourceAmount, actorId, request.ReversedAtUtc, reversal.Id));
        db.CashMovements.Add(CashMovement.TransferReversal(transfer.DestinationCashAccountId, -transfer.DestinationAmount, actorId, request.ReversedAtUtc, reversal.Id));
        if (transfer.FeeCashAccountId.HasValue)
            db.CashMovements.Add(CashMovement.TransferReversal(transfer.FeeCashAccountId.Value, transfer.FeeAmount!.Value, actorId, request.ReversedAtUtc, reversal.Id));
        await db.SaveChangesAsync(cancellationToken);
        var accounts = await TransferAccountsAsync(transfer, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(transfer, reversal, accounts);
    }

    public async Task<IReadOnlyList<CashTransferDto>> ListTransfersAsync(int year, int month,
        CancellationToken cancellationToken = default)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var start = new DateOnly(year, month, 1); var end = start.AddMonths(1);
        var transfers = await db.CashTransfers.AsNoTracking().Where(x => x.TransferredOn >= start && x.TransferredOn < end)
            .OrderByDescending(x => x.TransferredOn).ThenByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        var ids = transfers.Select(x => x.Id).ToArray();
        var reversals = await db.CashTransferReversals.AsNoTracking().Where(x => ids.Contains(x.CashTransferId))
            .ToDictionaryAsync(x => x.CashTransferId, cancellationToken);
        var accountIds = transfers.SelectMany(x => new[] { x.SourceCashAccountId, x.DestinationCashAccountId, x.FeeCashAccountId })
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        var accounts = await db.CashAccounts.AsNoTracking().Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        return transfers.Select(x => Map(x, reversals.GetValueOrDefault(x.Id), accounts)).ToArray();
    }

    public async Task<CashReconciliationDayDto> GetReconciliationDayAsync(DateOnly businessDate,
        CancellationToken cancellationToken = default)
    {
        var accounts = await db.CashAccounts.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Kind).ThenBy(x => x.Currency).ToListAsync(cancellationToken);
        var sameDayReconciliations = await db.CashReconciliations.AsNoTracking()
            .Where(x => x.BusinessDate == businessDate).ToListAsync(cancellationToken);
        var reconciliations = EffectiveByAccount(sameDayReconciliations);
        var corrections = await db.CashReconciliationCorrections.AsNoTracking()
            .Where(x => x.CashReconciliation.BusinessDate <= businessDate)
            .OrderByDescending(x => x.CorrectedAtUtc).ToListAsync(cancellationToken);
        var latestCorrectionByReconciliation = corrections.GroupBy(x => x.CashReconciliationId)
            .ToDictionary(x => x.Key, x => x.First());
        var prior = await db.CashReconciliations.AsNoTracking().Where(x => x.BusinessDate < businessDate)
            .OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.ClosedAtUtc).ToListAsync(cancellationToken);
        var previousByAccount = EffectivePreviousByAccount(prior);
        var (startUtc, endUtc) = BusinessDayRange(businessDate);
        var dayMovements = await db.CashMovements.AsNoTracking().Where(x => x.OccurredAtUtc >= startUtc && x.OccurredAtUtc < endUtc)
            .GroupBy(x => x.CashAccountId).Select(x => new { AccountId = x.Key, Amount = x.Sum(y => y.AmountChange) })
            .ToDictionaryAsync(x => x.AccountId, x => x.Amount, cancellationToken);
        var postClose = await db.CashMovements.AsNoTracking()
            .Where(x => x.OccurredAtUtc >= startUtc && x.OccurredAtUtc < endUtc)
            .ToListAsync(cancellationToken);

        var result = new List<CashReconciliationAccountDto>(accounts.Count);
        foreach (var account in accounts)
        {
            reconciliations.TryGetValue(account.Id, out var closed);
            previousByAccount.TryGetValue(account.Id, out var previous);
            var lateMovements = closed is null ? Array.Empty<CashMovement>() : postClose.Where(x => x.CashAccountId == account.Id && x.CreatedAtUtc > closed.ClosedAtUtc).ToArray();
            var late = (Amount: lateMovements.Sum(x => x.AmountChange), Count: lateMovements.Length);
            latestCorrectionByReconciliation.TryGetValue(closed?.Id ?? Guid.Empty, out var closedCorrection);
            latestCorrectionByReconciliation.TryGetValue(previous?.Id ?? Guid.Empty, out var previousCorrection);
            var previousCounted = previousCorrection?.CountedAmount ?? previous?.CountedAmount;
            var carryForward = previous is null ? 0m : await CashNetAsync(account.Id, BusinessDayRange(previous.BusinessDate).EndUtc, startUtc, cancellationToken);
            var opening = closed?.OpeningAmount ?? (previousCounted + carryForward);
            var source = closed?.OpeningSource ?? (previous is null ? null : CashReconciliationOpeningSource.PreviousClose);
            var expected = closedCorrection?.ExpectedClosingAmount ??
                (closed is { IsPartialDay: true } ? (opening.HasValue ? opening.Value + dayMovements.GetValueOrDefault(account.Id) : null) :
                    closed?.ExpectedClosingAmount ?? (opening.HasValue ? opening.Value + dayMovements.GetValueOrDefault(account.Id) : null));
            result.Add(new CashReconciliationAccountDto(account.Id, account.Code, account.Name, account.Currency, account.Kind,
                opening, source, expected, closedCorrection?.CountedAmount ?? closed?.CountedAmount ?? previousCorrection?.CountedAmount ?? previous?.CountedAmount,
                closed is null ? null : Map(closed, closedCorrection), late.Amount, late.Count, late.Count > 0));
        }
        return new(businessDate, result);
    }

    public async Task<CashReconciliationDto> ReconcileAsync(Guid accountId, PostCashReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        EnsureNotFutureBusinessDate(request.BusinessDate);
        var fingerprint = Fingerprint(accountId, request.BusinessDate, request.OpeningAmount?.ToString("G29", CultureInfo.InvariantCulture),
            request.CountedAmount.ToString("G29", CultureInfo.InvariantCulture), request.DifferenceReason?.Trim(), request.CountedAtUtc.ToUniversalTime().Ticks.ToString());
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);

        var replay = await db.CashReconciliations.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
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
        var sameDay = await db.CashReconciliations.Where(x => x.CashAccountId == accountId && x.BusinessDate == request.BusinessDate)
            .OrderByDescending(x => x.SupersedesCashReconciliationId.HasValue).ThenByDescending(x => x.ClosedAtUtc).ToListAsync(cancellationToken);
        var existingFinal = sameDay.FirstOrDefault(x => x.SupersedesCashReconciliationId.HasValue);
        if (existingFinal is not null)
            throw new ConflictException($"{account.Name} already has a final close for {request.BusinessDate:yyyy-MM-dd}.");
        var partial = sameDay.SingleOrDefault();

        var previous = await db.CashReconciliations.Where(x => x.CashAccountId == accountId && x.BusinessDate < request.BusinessDate)
            .OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.SupersedesCashReconciliationId.HasValue).ThenByDescending(x => x.ClosedAtUtc).FirstOrDefaultAsync(cancellationToken);
        decimal opening;
        CashReconciliationOpeningSource source;
        Guid? supersedes = null;
        var isFinalizingPartial = partial is not null;
        if (partial is not null)
        {
            if (!partial.IsPartialDay)
                throw new ConflictException($"{account.Name} is already reconciled for {request.BusinessDate:yyyy-MM-dd}.");
            if (request.BusinessDate >= BeirutToday())
                throw new ConflictException("A partial close can be finalized only after the Beirut business day has ended.");
            if (request.OpeningAmount.HasValue && request.OpeningAmount.Value != partial.OpeningAmount)
                throw new ConflictException("The final close must retain the partial close's opening amount.");
            opening = partial.OpeningAmount;
            source = partial.OpeningSource;
            supersedes = partial.Id;
        }
        else if (previous is not null)
        {
            if (previous.IsPartialDay)
                throw new ConflictException("Finalize the previous partial close before closing a later business day.");
            var previousCorrection = await db.CashReconciliationCorrections.Where(x => x.CashReconciliationId == previous.Id)
                .OrderByDescending(x => x.CorrectedAtUtc).FirstOrDefaultAsync(cancellationToken);
            var (_, previousEndUtc) = BusinessDayRange(previous.BusinessDate);
            var (nextStartUtc, _) = BusinessDayRange(request.BusinessDate);
            opening = (previousCorrection?.CountedAmount ?? previous.CountedAmount) +
                await CashNetAsync(accountId, previousEndUtc, nextStartUtc, cancellationToken);
            source = CashReconciliationOpeningSource.PreviousClose;
            if (request.OpeningAmount.HasValue && request.OpeningAmount.Value != opening)
                throw new ConflictException("This account already has a prior close; its opening must come from that counted amount.");
        }
        else
        {
            if (!request.OpeningAmount.HasValue)
                throw new ConflictException("An admin-entered opening amount is required until this account has a prior close.");
            opening = request.OpeningAmount.Value;
            source = CashReconciliationOpeningSource.AdminCount;
        }

        var (startUtc, endUtc) = BusinessDayRange(request.BusinessDate);
        var movementNet = await db.CashMovements.Where(x => x.CashAccountId == accountId && x.OccurredAtUtc >= startUtc && x.OccurredAtUtc < endUtc)
            .Select(x => (decimal?)x.AmountChange).SumAsync(cancellationToken) ?? 0m;
        var closedAtUtc = DateTime.UtcNow;
        var reconciliation = CashReconciliation.Create(accountId, request.BusinessDate, opening, source,
            opening + movementNet, request.CountedAmount, request.DifferenceReason, request.CountedAtUtc,
            closedAtUtc, actorId, !isFinalizingPartial && IsPartialBusinessDay(request.BusinessDate), supersedes,
            request.OperationId, fingerprint);
        db.CashReconciliations.Add(reconciliation);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(reconciliation);
    }

    public async Task<CashReconciliationCorrectionDto> CorrectReconciliationAsync(Guid reconciliationId,
        PostCashReconciliationCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var actorId = Actor();
        var fingerprint = Fingerprint(reconciliationId, request.CountedAmount.ToString("G29", CultureInfo.InvariantCulture),
            request.Reason.Trim(), request.CountedAtUtc.ToUniversalTime().Ticks.ToString());
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockOperation(request.OperationId, cancellationToken);
        var replay = await db.CashReconciliationCorrections.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
        if (replay is not null)
        {
            if (replay.CorrectedByUserId != actorId || replay.PayloadFingerprint != fingerprint)
                throw new ConflictException("This operation ID is already used by another cash command.");
            await tx.CommitAsync(cancellationToken);
            return Map(replay);
        }
        await EnsureOperationUnusedElsewhere(request.OperationId, cancellationToken);
        var reconciliation = await db.CashReconciliations.SingleOrDefaultAsync(x => x.Id == reconciliationId, cancellationToken)
            ?? throw new NotFoundException("Cash reconciliation", reconciliationId);
        if (reconciliation.IsPartialDay)
            throw new ConflictException("A partial close is finalized by posting its linked end-of-day close, not by correction.");
        EnsureNotFutureBusinessDate(reconciliation.BusinessDate);
        var (startUtc, endUtc) = BusinessDayRange(reconciliation.BusinessDate);
        var movementNet = await CashNetAsync(reconciliation.CashAccountId, startUtc, endUtc, cancellationToken);
        var correction = CashReconciliationCorrection.Create(reconciliation.Id, reconciliation.OpeningAmount + movementNet,
            request.CountedAmount, request.Reason, request.CountedAtUtc, DateTime.UtcNow, actorId,
            IsPartialBusinessDay(reconciliation.BusinessDate), request.OperationId, fingerprint);
        db.CashReconciliationCorrections.Add(correction);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return Map(correction);
    }

    private Guid Actor() => currentUser.UserId ?? throw new UnauthorizedAccessException();
    private IQueryable<OutgoingPayment> PaymentQuery() => db.OutgoingPayments.AsNoTracking()
        .Include(x => x.ExpenseCategory).Include(x => x.CashAccount);
    private IQueryable<SupplierPayment> SupplierPaymentQuery() => db.SupplierPayments.AsNoTracking()
        .Include(x => x.Supplier).Include(x => x.CashAccount).Include(x => x.Lines);

    private async Task LockOperation(Guid operationId, CancellationToken cancellationToken)
    {
        await OperationGuard.LockAsync(db, operationId, "Cash", cancellationToken);
    }

    private async Task EnsureOperationUnusedElsewhere(Guid operationId, CancellationToken cancellationToken)
    {
        if (await db.CashOpeningBalances.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.CashReconciliations.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.CashReconciliationCorrections.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.OutgoingPayments.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.OutgoingPaymentReversals.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.SupplierPayments.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.SupplierPaymentReversals.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.CashTransfers.AnyAsync(x => x.OperationId == operationId, cancellationToken) ||
            await db.CashTransferReversals.AnyAsync(x => x.OperationId == operationId, cancellationToken))
            throw new ConflictException("This operation ID is already used by another cash command.");
    }

    private static string PaymentFingerprint(PostOutgoingPaymentRequest request) => Fingerprint(
        request.ExpenseCategoryId, request.FundingSource, request.CashAccountId,
        request.OriginalAmount.ToString("G29", CultureInfo.InvariantCulture), request.Currency,
        request.AgreedUsdValue.ToString("G29", CultureInfo.InvariantCulture), request.PaidOn,
        request.Payee?.Trim(), request.Description?.Trim(), request.ForPeriod?.Trim(), request.ReceiptReference?.Trim());

    private static string SupplierPaymentFingerprint(PostSupplierPaymentRequest request) => Fingerprint(request.SupplierId,
        request.CashAccountId, request.PaidOn, request.Note?.Trim(), request.ReceiptReference?.Trim(),
        string.Join(";", request.Lines.Select(x => $"{x.Kind}:{x.OriginalAmount:G29}:{x.Currency}:{x.AgreedUsdValue:G29}")));

    private static string TransferFingerprint(PostCashTransferRequest request) => Fingerprint(request.SourceCashAccountId,
        request.DestinationCashAccountId, request.TransferredOn, request.SourceAmount.ToString("G29", CultureInfo.InvariantCulture),
        request.SourceCurrency, request.DestinationAmount.ToString("G29", CultureInfo.InvariantCulture), request.DestinationCurrency,
        request.AgreedRate.ToString("G29", CultureInfo.InvariantCulture), request.FeeCashAccountId,
        request.FeeAmount?.ToString("G29", CultureInfo.InvariantCulture), request.FeeCurrency, request.Note?.Trim());

    private static void ValidateSupplierPaymentLines(IReadOnlyList<SupplierPaymentLineRequest> lines)
    {
        if (lines.Count is < 1 or > 2 || lines.Count(x => x.Kind == SupplierPaymentLineKind.Settlement) != 1 ||
            lines.Count(x => x.Kind == SupplierPaymentLineKind.Fee) > 1 || lines.Any(x => x.OriginalAmount <= 0 || x.AgreedUsdValue <= 0))
            throw new ValidationException("A supplier payment requires one positive settlement line and may include one separate positive fee line.");
    }

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

    private static SupplierPaymentDto Map(SupplierPayment payment, SupplierPaymentReversal? reversal,
        Supplier? supplier = null, CashAccount? account = null) => new(payment.Id, payment.OperationId, payment.SupplierId,
            (supplier ?? payment.Supplier).Name, payment.CashAccountId, (account ?? payment.CashAccount).Name, payment.PaidOn,
            payment.Note, payment.ReceiptReference, payment.CreatedAtUtc, payment.RecordedByUserId,
            payment.Lines.Select(x => new SupplierPaymentLineDto(x.Id, x.Kind, x.OriginalAmount, x.Currency, x.AgreedUsdValue)).ToArray(),
            reversal is not null, reversal?.Id, reversal?.Reason, reversal?.ReversedAtUtc);

    private static CashTransferDto Map(CashTransfer transfer, CashTransferReversal? reversal,
        IReadOnlyDictionary<Guid, CashAccount> accounts) => new(transfer.Id, transfer.OperationId,
            transfer.SourceCashAccountId, accounts[transfer.SourceCashAccountId].Name, transfer.DestinationCashAccountId,
            accounts[transfer.DestinationCashAccountId].Name, transfer.TransferredOn, transfer.SourceAmount,
            transfer.SourceCurrency, transfer.DestinationAmount, transfer.DestinationCurrency, transfer.AgreedRate,
            transfer.FeeCashAccountId, transfer.FeeCashAccountId.HasValue ? accounts[transfer.FeeCashAccountId.Value].Name : null,
            transfer.FeeAmount, transfer.FeeCurrency, transfer.Note, transfer.CreatedAtUtc, transfer.RecordedByUserId,
            reversal is not null, reversal?.Id, reversal?.Reason, reversal?.ReversedAtUtc);

    private async Task<Dictionary<Guid, CashAccount>> TransferAccountsAsync(CashTransfer transfer, CancellationToken cancellationToken)
    {
        var ids = new[] { transfer.SourceCashAccountId, transfer.DestinationCashAccountId, transfer.FeeCashAccountId }
            .Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToArray();
        return await db.CashAccounts.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private static CashReconciliationDto Map(CashReconciliation reconciliation, CashReconciliationCorrection? correction = null) => new(reconciliation.Id,
        reconciliation.CashAccountId, reconciliation.BusinessDate, reconciliation.OpeningAmount,
        reconciliation.OpeningSource, reconciliation.ExpectedClosingAmount, reconciliation.CountedAmount,
        reconciliation.DifferenceAmount, reconciliation.DifferenceReason, reconciliation.CountedAtUtc,
        reconciliation.ClosedAtUtc, reconciliation.CountedByUserId, reconciliation.IsPartialDay, reconciliation.OperationId,
        reconciliation.CreatedAtUtc, correction is null ? null : Map(correction), reconciliation.SupersedesCashReconciliationId);

    private static CashReconciliationCorrectionDto Map(CashReconciliationCorrection correction) => new(correction.Id,
        correction.CashReconciliationId, correction.ExpectedClosingAmount, correction.CountedAmount,
        correction.DifferenceAmount, correction.Reason, correction.CountedAtUtc, correction.CorrectedAtUtc,
        correction.CorrectedByUserId, correction.IsPartialDay, correction.OperationId, correction.CreatedAtUtc);

    private static Dictionary<Guid, CashReconciliation> EffectiveByAccount(IEnumerable<CashReconciliation> rows) =>
        rows.GroupBy(x => x.CashAccountId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(x => x.SupersedesCashReconciliationId.HasValue).ThenByDescending(x => x.ClosedAtUtc).First());

    private static Dictionary<Guid, CashReconciliation> EffectivePreviousByAccount(IEnumerable<CashReconciliation> rows) =>
        rows.GroupBy(x => x.CashAccountId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.SupersedesCashReconciliationId.HasValue)
                .ThenByDescending(x => x.ClosedAtUtc).First());

    private async Task<decimal> CashNetAsync(Guid accountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken) =>
        await db.CashMovements.Where(x => x.CashAccountId == accountId && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .Select(x => (decimal?)x.AmountChange).SumAsync(cancellationToken) ?? 0m;

    private static void EnsureNotFutureBusinessDate(DateOnly businessDate)
    {
        if (businessDate > BeirutToday())
            throw new ValidationException("A cash reconciliation cannot be posted for a future Beirut business day.");
    }

    private static bool IsPartialBusinessDay(DateOnly businessDate) => businessDate == BeirutToday();

    private static DateTime BusinessDayMomentUtc(DateOnly businessDate) => BusinessDayRange(businessDate).StartUtc.AddHours(12);

    private static DateOnly BeirutToday() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BeirutZone()));

    private static (DateTime StartUtc, DateTime EndUtc) BusinessDayRange(DateOnly businessDate)
    {
        var zone = BeirutZone();
        var start = TimeZoneInfo.ConvertTimeToUtc(businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(businessDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        return (start, end);
    }

    private static TimeZoneInfo BeirutZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Middle East Standard Time"); }
    }
}
