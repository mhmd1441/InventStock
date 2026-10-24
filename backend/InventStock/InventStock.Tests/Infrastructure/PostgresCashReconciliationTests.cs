using InventStock.Application.DTOs.Cash;
using InventStock.Application.Common.Exceptions;
using FluentValidation;
using InventStock.Domain.Enums;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventStock.Tests.Infrastructure;

[Collection("PostgreSQL schema migrations")]
public sealed class PostgresCashReconciliationTests(PostgresSaleFixture fixture) : IClassFixture<PostgresSaleFixture>
{
    [Fact]
    public async Task Beirut_day_close_snapshots_difference_replays_and_flags_late_same_day_entries()
    {
        var account = fixture.Accounts["USD"];
        var date = new DateOnly(2026, 1, 2); // Beirut is UTC+2 in January: start is 2026-01-01T22:00Z.
        var start = new DateTime(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc);
        await using var db = fixture.Open();
        db.CashMovements.AddRange(
            CashMovement.Opening(account.Id, 20, fixture.Actor, start.AddMinutes(1), Guid.NewGuid()),
            CashMovement.Opening(account.Id, 99, fixture.Actor, start.AddMinutes(-1), Guid.NewGuid()),
            CashMovement.Opening(account.Id, 77, fixture.Actor, start.AddDays(1), Guid.NewGuid()));
        await db.SaveChangesAsync();

        var service = fixture.CashService(db, fixture.AdminActor);
        var request = new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = date, OpeningAmount = 100,
            CountedAmount = 115, DifferenceReason = "Five dollars short", CountedAtUtc = DateTime.UtcNow
        };
        var posted = await service.ReconcileAsync(account.Id, request);
        Assert.Equal(120, posted.ExpectedClosingAmount);
        Assert.Equal(-5, posted.DifferenceAmount);
        Assert.Equal("Five dollars short", posted.DifferenceReason);
        Assert.Equal(posted.Id, (await service.ReconcileAsync(account.Id, request)).Id);
        Assert.Equal(1, await db.CashReconciliations.CountAsync(x => x.OperationId == request.OperationId));

        db.CashMovements.Add(CashMovement.Opening(account.Id, 7, fixture.Actor, start.AddMinutes(2), Guid.NewGuid()));
        await db.SaveChangesAsync();
        var day = await service.GetReconciliationDayAsync(date);
        var status = day.Accounts.Single(x => x.CashAccountId == account.Id);
        Assert.Equal(120, status.ExpectedAmount); // close snapshot is immutable.
        Assert.Equal(115, status.LastCountedAmount);
        Assert.True(status.RequiresExplicitCorrection);
        Assert.Equal(7, status.PostCloseNetChange);
        Assert.Equal(1, status.PostCloseMovementCount);

        var correctionRequest = new PostCashReconciliationCorrectionRequest
        {
            OperationId = Guid.NewGuid(), CountedAmount = 127, Reason = "Counted again after the late entry", CountedAtUtc = DateTime.UtcNow
        };
        var correction = await service.CorrectReconciliationAsync(posted.Id, correctionRequest);
        Assert.Equal(127, correction.ExpectedClosingAmount);
        Assert.Equal(0, correction.DifferenceAmount);
        Assert.Equal(correction.Id, (await service.CorrectReconciliationAsync(posted.Id, correctionRequest)).Id);
        Assert.Equal(115, (await db.CashReconciliations.SingleAsync(x => x.Id == posted.Id)).CountedAmount); // Original close is immutable.
        status = (await service.GetReconciliationDayAsync(date)).Accounts.Single(x => x.CashAccountId == account.Id);
        Assert.Equal(127, status.ExpectedAmount);
        Assert.Equal(127, status.LastCountedAmount);
        Assert.Equal(correction.Id, status.Reconciliation!.LatestCorrection!.Id);

        await Assert.ThrowsAsync<ConflictException>(() => service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = date, OpeningAmount = 100, CountedAmount = 127, CountedAtUtc = DateTime.UtcNow
        }));
    }

    [Fact]
    public async Task Previous_close_becomes_next_day_opening_without_rewriting_history()
    {
        var account = fixture.Accounts["EUR"];
        var firstDay = new DateOnly(2026, 2, 1);
        await using var db = fixture.Open();
        var service = fixture.CashService(db, fixture.AdminActor);
        await service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = firstDay, OpeningAmount = 30,
            CountedAmount = 31, DifferenceReason = "One euro found", CountedAtUtc = DateTime.UtcNow
        });
        // A skipped business day still carries its recorded movement into the next day's opening.
        var gapMovement = new DateTime(2026, 2, 2, 12, 0, 0, DateTimeKind.Utc);
        db.CashMovements.Add(CashMovement.Opening(account.Id, 4, fixture.Actor, gapMovement, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var next = await service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = firstDay.AddDays(3), CountedAmount = 35,
            CountedAtUtc = DateTime.UtcNow
        });
        Assert.Equal(35, next.OpeningAmount);
        Assert.Equal(CashReconciliationOpeningSource.PreviousClose, next.OpeningSource);
        Assert.Equal(35, next.ExpectedClosingAmount);

        await Assert.ThrowsAsync<ConflictException>(() => service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = firstDay.AddDays(4), OpeningAmount = 31, CountedAmount = 35,
            CountedAtUtc = DateTime.UtcNow
        }));
    }

    [Fact]
    public async Task Current_day_close_is_marked_partial_and_future_days_are_rejected()
    {
        var account = fixture.Accounts["LBP"];
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        await using var db = fixture.Open();
        var service = fixture.CashService(db, fixture.AdminActor);
        var close = await service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = today, OpeningAmount = 0, CountedAmount = 0, CountedAtUtc = DateTime.UtcNow
        });
        Assert.True(close.IsPartialDay);
        await Assert.ThrowsAsync<ValidationException>(() => service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = today.AddDays(1), OpeningAmount = 0, CountedAmount = 0, CountedAtUtc = DateTime.UtcNow
        }));
    }

    [Fact]
    public async Task End_of_day_close_supersedes_a_partial_close_without_using_a_correction()
    {
        var account = fixture.Accounts["USD"];
        var date = new DateOnly(2026, 2, 10);
        await using var db = fixture.Open();
        var partial = CashReconciliation.Create(account.Id, date, 100m, CashReconciliationOpeningSource.AdminCount,
            100m, 100m, null, DateTime.UtcNow, DateTime.UtcNow, fixture.AdminActor,
            true, null, Guid.NewGuid(), new string('A', 64));
        db.CashReconciliations.Add(partial);
        var (startUtc, _) = BeirutRange(date);
        db.CashMovements.Add(CashMovement.Opening(account.Id, 20m, fixture.Actor, startUtc.AddHours(14), Guid.NewGuid()));
        await db.SaveChangesAsync();

        var service = fixture.CashService(db, fixture.AdminActor);
        var final = await service.ReconcileAsync(account.Id, new PostCashReconciliationRequest
        {
            OperationId = Guid.NewGuid(), BusinessDate = date, CountedAmount = 120m, CountedAtUtc = DateTime.UtcNow
        });

        Assert.False(final.IsPartialDay);
        Assert.Equal(partial.Id, final.SupersedesReconciliationId);
        Assert.Equal(120m, final.ExpectedClosingAmount);
        Assert.Equal(100m, (await db.CashReconciliations.SingleAsync(x => x.Id == partial.Id)).CountedAmount);
        Assert.Equal(2, await db.CashReconciliations.CountAsync(x => x.CashAccountId == account.Id && x.BusinessDate == date));
        await Assert.ThrowsAsync<ConflictException>(() => service.CorrectReconciliationAsync(partial.Id,
            new PostCashReconciliationCorrectionRequest { OperationId = Guid.NewGuid(), CountedAmount = 100m, Reason = "Not a correction", CountedAtUtc = DateTime.UtcNow }));
    }

    private static (DateTime StartUtc, DateTime EndUtc) BeirutRange(DateOnly date)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Beirut");
        var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);
        return (start, end);
    }
}
