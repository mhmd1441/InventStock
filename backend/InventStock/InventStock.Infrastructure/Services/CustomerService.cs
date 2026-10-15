using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using InventStock.Application.Common.Exceptions;
using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Customers;
using InventStock.Application.DTOs.Sales;
using InventStock.Application.Interfaces;
using InventStock.Application.Validators;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Infrastructure.Services;

public sealed class CustomerService(AppDbContext db, ICurrentUserService user) : ICustomerService
{
    public Task<CustomerProfileDto> CreateAsync(SaveCustomerRequest request, CancellationToken ct = default) => SaveProfile(null, request, ct);
    public Task<CustomerProfileDto> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken ct = default) => SaveProfile(id, request, ct);

    private async Task<CustomerProfileDto> SaveProfile(Guid? id, SaveCustomerRequest request, CancellationToken ct)
    {
        await new SaveCustomerRequestValidator().ValidateAndThrowAsync(request, ct);
        var fingerprint = Hash(new { id, Name = request.Name.Trim(), Label = Clean(request.DistinguishingLabel), Phone = Clean(request.Phone), Address = Clean(request.Address), request.ExpectedRevision });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationGuard.LockAsync(db, request.OperationId, "CustomerProfile", ct);
        var actor = await Authorize(ct);
        var replay = await db.CustomerProfileEvents.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
        if (replay is not null)
        {
            CheckReplay(replay.ActorId, actor, replay.PayloadFingerprint, fingerprint);
            return Map(replay);
        }
        Customer customer;
        if (id.HasValue)
        {
            customer = await LockCustomer(id.Value, ct);
            if (customer.Revision != request.ExpectedRevision) throw new ConflictException("Customer details changed. Reload before saving.");
        }
        else
        {
            if (request.ExpectedRevision != 0) throw new ConflictException("New customers must start at revision zero.");
            customer = Customer.Create(request.Name);
            db.Customers.Add(customer);
        }
        customer.UpdateProfile(request.Name, Clean(request.DistinguishingLabel), Clean(request.Phone), Clean(request.Address));
        var snapshot = CustomerProfileEvent.Create(customer, request.OperationId, actor, fingerprint);
        db.CustomerProfileEvents.Add(snapshot);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(snapshot);
    }

    public async Task<CustomerProfileDto> GetProfileOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var actor = await Authorize(ct);
        return Map(await db.CustomerProfileEvents.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Customer profile operation", operationId));
    }

    public async Task<PagedResult<CustomerSummaryDto>> SearchAsync(CustomerQuery query, CancellationToken ct = default)
    {
        await Authorize(ct);
        ValidatePage(query);
        if (query.Search?.Length > 200) throw new ValidationException("Customer search is limited to 200 characters.");
        var source = db.Customers.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            source = source.Where(x => x.Name.ToUpper().Contains(term) ||
                (x.DistinguishingLabel != null && x.DistinguishingLabel.ToUpper().Contains(term)) ||
                (x.Phone != null && x.Phone.Contains(term)));
        }
        var count = await source.CountAsync(ct);
        var customers = await source.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(query.Skip).Take(query.PageSize).ToListAsync(ct);
        var ids = customers.Select(x => x.Id).ToArray();
        var balances = await db.CustomerAccountEntries.Where(x => ids.Contains(x.CustomerId)).GroupBy(x => x.CustomerId)
            .Select(g => new { Id = g.Key, Balance = g.Sum(x => x.AmountUsd) }).ToDictionaryAsync(x => x.Id, ct);
        return new(customers.Select(x => new CustomerSummaryDto(Map(x), Normalize(balances.GetValueOrDefault(x.Id)?.Balance ?? 0))).ToArray(), count, query.Page, query.PageSize);
    }

    public async Task<CustomerStatementDto> StatementAsync(Guid id, CustomerStatementQuery query, CancellationToken ct = default)
    {
        await Authorize(ct);
        ValidatePage(query);
        // Header balance and the page are one consistent snapshot, even if another counter takes a payment.
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Customer", id);
        var entries = db.CustomerAccountEntries.AsNoTracking().Where(x => x.CustomerId == id);
        var balance = await Balance(id, ct);
        var count = await entries.CountAsync(ct);
        var rows = await entries.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id)
            .Skip(query.Skip).Take(query.PageSize).Select(x => new CustomerStatementEntryDto(x.Id, x.Kind, x.AmountUsd,
                x.SaleId, x.CustomerPaymentId, x.CustomerPaymentCorrectionId, x.OperationId, x.ActorId,
                x.CreatedAtUtc, x.EffectiveAtUtc, x.Reason, x.OriginalOpeningEntryId,
                x.CorrectedOpeningBalanceUsd, x.SaleReturnId)).ToListAsync(ct);
        await tx.CommitAsync(ct);
        return new(new(Map(customer), Normalize(balance)), new(rows, count, query.Page, query.PageSize));
    }

    public async Task<CustomerOpeningDto> OpenAccountAsync(Guid id, PostCustomerOpeningRequest request, CancellationToken ct = default)
    {
        await new PostCustomerOpeningRequestValidator().ValidateAndThrowAsync(request, ct);
        var time = Microseconds(request.AsOfUtc);
        var fingerprint = Hash(new { id, Balance = Number(request.BalanceUsd), time, Reason = request.Reason.Trim() });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationGuard.LockAsync(db, request.OperationId, "CustomerOpening", ct);
        var actor = await Authorize(ct, true);
        var replay = await db.CustomerAccountEntries.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId && x.Kind == "Opening", ct);
        if (replay is not null)
        {
            CheckReplay(replay.ActorId, actor, replay.PayloadFingerprint, fingerprint);
            return MapOpening(replay);
        }
        await LockCustomer(id, ct);
        if (await db.CustomerAccountEntries.AnyAsync(x => x.CustomerId == id, ct))
            throw new ConflictException("Opening balances must be reviewed before the first account transaction. This account already has history.");
        var opening = CustomerAccountEntry.Opening(id, request.OperationId, actor, request.BalanceUsd);
        opening.DescribeOpening(request.Reason, time, fingerprint);
        db.CustomerAccountEntries.Add(opening);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MapOpening(opening);
    }

    public async Task<CustomerOpeningDto> GetOpeningOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var actor = await Authorize(ct, true);
        return MapOpening(await db.CustomerAccountEntries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == operationId && x.Kind == "Opening" && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Customer opening operation", operationId));
    }

    public async Task<CustomerOpeningStatusDto?> OpeningStatusAsync(Guid customerId, CancellationToken ct = default)
    {
        await Authorize(ct, true);
        if (!await db.Customers.AsNoTracking().AnyAsync(x => x.Id == customerId && x.IsActive, ct))
            throw new NotFoundException("Active customer", customerId);
        var opening = await db.CustomerAccountEntries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CustomerId == customerId && x.Kind == "Opening", ct);
        if (opening is null) return null;
        var adjustment = await db.CustomerAccountEntries.AsNoTracking()
            .Where(x => x.OriginalOpeningEntryId == opening.Id && x.Kind == "OpeningCorrection")
            .SumAsync(x => (decimal?)x.AmountUsd, ct) ?? 0;
        return new(opening.Id, Normalize(opening.AmountUsd), Normalize(opening.AmountUsd + adjustment),
            opening.EffectiveAtUtc ?? opening.CreatedAtUtc);
    }

    public async Task<CustomerOpeningCorrectionDto> CorrectOpeningAsync(Guid openingEntryId,
        CorrectCustomerOpeningRequest request, CancellationToken ct = default)
    {
        await new CorrectCustomerOpeningRequestValidator().ValidateAndThrowAsync(request, ct);
        var fingerprint = Hash(new { openingEntryId, Expected = Number(request.ExpectedCurrentOpeningBalanceUsd),
            Corrected = Number(request.CorrectedOpeningBalanceUsd),
            BalanceAfter = Number(request.ConfirmedBalanceAfterUsd), Reason = request.Reason.Trim() });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationGuard.LockAsync(db, request.OperationId, "CustomerOpeningCorrection", ct);
        var actor = await Authorize(ct, true);
        var replay = await db.CustomerAccountEntries.AsNoTracking().SingleOrDefaultAsync(
            x => x.OperationId == request.OperationId && x.Kind == "OpeningCorrection", ct);
        if (replay is not null)
        {
            CheckReplay(replay.ActorId, actor, replay.PayloadFingerprint, fingerprint);
            return MapOpeningCorrection(replay);
        }
        var opening = await db.CustomerAccountEntries.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == openingEntryId && x.Kind == "Opening", ct)
            ?? throw new NotFoundException("Customer opening", openingEntryId);
        await LockCustomer(opening.CustomerId, ct);
        var previousCorrections = await db.CustomerAccountEntries
            .Where(x => x.OriginalOpeningEntryId == opening.Id && x.Kind == "OpeningCorrection")
            .SumAsync(x => (decimal?)x.AmountUsd, ct) ?? 0;
        if (opening.AmountUsd + previousCorrections != request.ExpectedCurrentOpeningBalanceUsd)
            throw new ConflictException("The opening balance changed. Reload before correcting it.");
        var delta = request.CorrectedOpeningBalanceUsd - opening.AmountUsd - previousCorrections;
        if (delta == 0) throw new ConflictException("The opening balance already has this value.");
        var balanceAfter = await Balance(opening.CustomerId, ct) + delta;
        if (balanceAfter != request.ConfirmedBalanceAfterUsd)
            throw new ConflictException("The customer's balance changed. Review the resulting balance before confirming the correction.");
        var correction = CustomerAccountEntry.OpeningCorrection(opening, request.OperationId, actor,
            request.CorrectedOpeningBalanceUsd, delta, balanceAfter, request.Reason, fingerprint);
        db.CustomerAccountEntries.Add(correction);
        await db.SaveChangesAsync(ct);
        var result = MapOpeningCorrection(correction);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<CustomerOpeningCorrectionDto> GetOpeningCorrectionOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var actor = await Authorize(ct, true);
        return MapOpeningCorrection(await db.CustomerAccountEntries.AsNoTracking().SingleOrDefaultAsync(
            x => x.OperationId == operationId && x.Kind == "OpeningCorrection" && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Customer opening correction", operationId));
    }

    public async Task<CustomerPaymentDto> PayAsync(Guid id, PostCustomerPaymentRequest request, CancellationToken ct = default)
    {
        await new PostCustomerPaymentRequestValidator().ValidateAndThrowAsync(request, ct);
        var fingerprint = Hash(new { id, Usd = Number(request.ConfirmedUsdCredit), Excess = Number(request.ConfirmedExcessCreditUsd),
            Payments = CanonicalTenders(request.Payments), Change = CanonicalTenders(request.Change), Note = Clean(request.Note) });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationGuard.LockAsync(db, request.OperationId, "CustomerPayment", ct);
        var actor = await Authorize(ct);
        var replay = await PaymentQuery().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
        if (replay is not null)
        {
            CheckReplay(replay.ActorId, actor, replay.PayloadFingerprint, fingerprint);
            return Map(replay);
        }
        var customer = await LockCustomer(id, ct);
        var balance = await Balance(id, ct);
        var net = request.Payments.Sum(x => x.ConfirmedUsdAmount) - request.Change.Sum(x => x.ConfirmedUsdAmount);
        if (net != request.ConfirmedUsdCredit) throw new ConflictException("Received USD settlements minus explicit change must equal the confirmed account payment.");
        var excess = Math.Max(0, net - Math.Max(0, balance));
        if (request.ConfirmedExcessCreditUsd != excess)
            throw new ConflictException("Review the current customer balance and explicitly confirm the excess to keep as customer credit, or record change.");
        var payment = CustomerPayment.Create(customer, request.OperationId, actor, fingerprint, net, excess, balance,
            Clean(request.Note), Microseconds(DateTime.UtcNow));
        var tenders = request.Payments.Select(x => (Request: x, Change: false))
            .Concat(request.Change.Select(x => (Request: x, Change: true))).ToArray();
        var ids = tenders.Select(x => x.Request.CashAccountId).Distinct().ToArray();
        var accounts = await db.CashAccounts.AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        for (var i = 0; i < tenders.Length; i++)
        {
            var (requestTender, change) = tenders[i];
            if (!accounts.TryGetValue(requestTender.CashAccountId, out var account) || account.Currency != requestTender.Currency)
                throw new ConflictException("Each tender requires an active account matching its currency.");
            var tender = CustomerPaymentTender.Create(payment.Id, i, account, change, requestTender.NativeAmount, requestTender.ConfirmedUsdAmount);
            payment.Tenders.Add(tender);
            db.CashMovements.Add(CashMovement.CustomerTender(tender, actor, payment.PostedAtUtc));
        }
        db.CustomerPayments.Add(payment);
        db.CustomerAccountEntries.Add(CustomerAccountEntry.Payment(id, payment.Id, request.OperationId, actor, net));
        await db.SaveChangesAsync(ct);
        var result = Map(payment);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<CustomerPaymentDto> GetPaymentOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var actor = await Authorize(ct);
        return Map(await PaymentQuery().SingleOrDefaultAsync(x => x.OperationId == operationId && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Customer payment operation", operationId));
    }

    public async Task<CustomerPaymentCorrectionDto> CorrectPaymentAsync(Guid paymentId, CorrectCustomerPaymentRequest request, CancellationToken ct = default)
    {
        await new CorrectCustomerPaymentRequestValidator().ValidateAndThrowAsync(request, ct);
        var fingerprint = Hash(new { paymentId, Reason = request.Reason.Trim(), BalanceAfter = Number(request.ConfirmedBalanceAfterUsd) });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await OperationGuard.LockAsync(db, request.OperationId, "CustomerCorrection", ct);
        var actor = await Authorize(ct); // Owner explicitly permits staff to correct mistaken payments.
        var replay = await db.CustomerPaymentCorrections.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, ct);
        if (replay is not null)
        {
            CheckReplay(replay.ActorId, actor, replay.PayloadFingerprint, fingerprint);
            return Map(replay);
        }
        var payment = await PaymentQuery().SingleOrDefaultAsync(x => x.Id == paymentId, ct)
            ?? throw new NotFoundException("Customer payment", paymentId);
        await LockCustomer(payment.CustomerId, ct);
        if (await db.CustomerPaymentCorrections.AnyAsync(x => x.OriginalPaymentId == paymentId, ct))
            throw new ConflictException("This payment already has a correction. It cannot be reversed twice.");
        var balanceAfter = await Balance(payment.CustomerId, ct) + payment.UsdCredit;
        if (request.ConfirmedBalanceAfterUsd != balanceAfter)
            throw new ConflictException("The customer's balance changed. Review the resulting balance before confirming the correction.");
        var correction = CustomerPaymentCorrection.Create(payment, request.OperationId, actor, fingerprint,
            request.Reason, balanceAfter, Microseconds(DateTime.UtcNow));
        db.CustomerPaymentCorrections.Add(correction);
        db.CustomerAccountEntries.Add(CustomerAccountEntry.PaymentCorrection(correction));
        foreach (var tender in payment.Tenders)
            db.CashMovements.Add(CashMovement.CustomerTender(tender, actor, correction.CorrectedAtUtc, correction.Id));
        await db.SaveChangesAsync(ct);
        var result = Map(correction);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<CustomerPaymentCorrectionDto> GetCorrectionOperationAsync(Guid operationId, CancellationToken ct = default)
    {
        var actor = await Authorize(ct);
        return Map(await db.CustomerPaymentCorrections.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == operationId && x.ActorId == actor, ct)
            ?? throw new NotFoundException("Customer payment correction", operationId));
    }

    private Task<Guid> Authorize(CancellationToken ct, bool admin = false) => CounterAuthorization.RequireAsync(db, user, ct, admin);
    private async Task<Customer> LockCustomer(Guid id, CancellationToken ct)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Customers\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        return await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct) ?? throw new NotFoundException("Active customer", id);
    }
    private async Task<decimal> Balance(Guid id, CancellationToken ct) =>
        await db.CustomerAccountEntries.Where(x => x.CustomerId == id).SumAsync(x => (decimal?)x.AmountUsd, ct) ?? 0;
    private IQueryable<CustomerPayment> PaymentQuery() => db.CustomerPayments.AsNoTracking().Include(x => x.Tenders);
    private static void ValidatePage(PagedQuery q) { if (q.Page > 1000000) throw new ValidationException("Page is too large."); }
    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string Number(decimal x) => x.ToString("G29", CultureInfo.InvariantCulture);
    private static decimal Normalize(decimal x) => decimal.Parse(Number(x), CultureInfo.InvariantCulture);
    private static DateTime Microseconds(DateTime time) => new(time.Ticks - time.Ticks % 10, DateTimeKind.Utc);
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static object CanonicalTenders(IReadOnlyList<SaleTenderRequest> rows) => rows.Select(x => new
        { x.CashAccountId, x.Currency, Native = Number(x.NativeAmount), Usd = Number(x.ConfirmedUsdAmount) }).ToArray();
    private static void CheckReplay(Guid originalActor, Guid actor, string? originalFingerprint, string fingerprint)
    {
        if (originalActor != actor || originalFingerprint != fingerprint) throw new ConflictException("This operation ID belongs to another actor or payload.");
    }
    private static CustomerProfileDto Map(Customer c) => new(c.Id, c.Name, c.DistinguishingLabel, c.Phone, c.Address, c.Revision);
    private static CustomerProfileDto Map(CustomerProfileEvent c) => new(c.CustomerId, c.Name, c.DistinguishingLabel, c.Phone, c.Address, c.Revision);
    private static CustomerOpeningDto MapOpening(CustomerAccountEntry e) => new(e.Id, e.CustomerId, e.OperationId, e.ActorId,
        Normalize(e.AmountUsd), e.EffectiveAtUtc ?? e.CreatedAtUtc, e.Reason ?? "Legacy opening balance");
    private static CustomerOpeningCorrectionDto MapOpeningCorrection(CustomerAccountEntry e) => new(e.Id,
        e.OriginalOpeningEntryId!.Value, e.CustomerId, e.OperationId, e.ActorId, Normalize(e.AmountUsd),
        Normalize(e.CorrectedOpeningBalanceUsd!.Value), Normalize(e.BalanceAfterUsd!.Value), Microseconds(e.CreatedAtUtc),
        e.Reason ?? "Opening balance correction");
    private static CustomerPaymentDto Map(CustomerPayment p) => new(p.Id, p.OperationId, p.CustomerId, p.CustomerName, p.ActorId,
        p.PostedAtUtc, Normalize(p.UsdCredit), Normalize(p.ExcessCreditUsd), Normalize(p.BalanceBeforeUsd), Normalize(p.BalanceAfterUsd), p.Note,
        p.Tenders.OrderBy(x => x.Position).Select(x => new SaleTenderDto(x.Id, x.CashAccountId, x.AccountName, x.Currency,
            x.IsChange, Normalize(x.NativeAmount), Normalize(x.ConfirmedUsdAmount))).ToArray());
    private static CustomerPaymentCorrectionDto Map(CustomerPaymentCorrection c) => new(c.Id, c.OperationId, c.CustomerId,
        c.OriginalPaymentId, c.ActorId, c.CorrectedAtUtc, c.Reason, Normalize(c.RestoredDebtUsd), Normalize(c.BalanceAfterUsd));
}
