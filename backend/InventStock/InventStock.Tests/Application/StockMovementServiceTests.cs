using InventStock.Application.Common.Exceptions;
using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using InventStock.Application.Services;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;
using InventStock.Infrastructure.Data;
using InventStock.Infrastructure.Identity;
using InventStock.Infrastructure.Repositories;
using InventStock.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventStock.Tests.Application;

public class StockMovementServiceTests
{
    private sealed class RecordingNotifier : IStockNotifier
    {
        public List<StockChangedNotification> Notifications { get; } = new();

        public Task StockChangedAsync(StockChangedNotification notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotifier : IStockNotifier
    {
        public Task StockChangedAsync(StockChangedNotification notification, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("hub unavailable");
    }

    private sealed class Harness : IDisposable
    {
        public Harness(IStockNotifier? notifier = null, int lowStockThreshold = 5)
        {
            Context = new SqliteTestContext();
            Notifier = notifier ?? new RecordingNotifier();

            Context.Db.Users.Add(new ApplicationUser
            {
                Id = Context.CurrentUser.UserId!.Value,
                UserName = "tester@inventstock.local",
                NormalizedUserName = "TESTER@INVENTSTOCK.LOCAL",
                Email = "tester@inventstock.local",
                NormalizedEmail = "TESTER@INVENTSTOCK.LOCAL",
                FullName = "Test User"
            });

            var category = Category.Create("Brakes", null);
            Context.Db.Categories.Add(category);
            Context.Db.SaveChanges();

            var part = Part.Create("BRK-1001", "Front Brake Pad", null, category.Id, null, 42.50m, lowStockThreshold, null);
            Context.Db.Parts.Add(part);
            Context.Db.SaveChanges();
            PartId = part.Id;

            var partRepository = new PartRepository(Context.Db);
            Service = new StockMovementService(
                partRepository,
                new StockMovementRepository(Context.Db),
                new UnitOfWork(Context.Db),
                Context.CurrentUser,
                Notifier,
                NullLogger<StockMovementService>.Instance);
        }

        public SqliteTestContext Context { get; }

        public IStockNotifier Notifier { get; }

        public StockMovementService Service { get; }

        public Guid PartId { get; }

        public AppDbContext Db => Context.Db;

        public void Dispose() => Context.Dispose();
    }

    [Fact]
    public async Task StockIn_IncreasesQuantityOnHand()
    {
        using var harness = new Harness();

        var movement = await harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 20 });

        Assert.Equal(MovementType.In, movement.MovementType);
        Assert.Equal(20, movement.QuantityChange);
        Assert.Equal("BRK-1001", movement.PartNumber);
    }

    [Fact]
    public async Task StockOut_BeyondAvailableStock_IsRejected()
    {
        using var harness = new Harness();
        await harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 5 });

        var exception = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            harness.Service.StockOutAsync(harness.PartId, new StockOutRequest { Quantity = 6 }));

        Assert.Equal(5, exception.Available);
        Assert.Equal(6, exception.Requested);
    }

    [Fact]
    public async Task RejectedStockOut_LeavesNoMovementBehind()
    {
        using var harness = new Harness();
        await harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 5 });

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            harness.Service.StockOutAsync(harness.PartId, new StockOutRequest { Quantity = 99 }));

        var movements = harness.Db.StockMovements.Count(m => m.PartId == harness.PartId);
        Assert.Equal(1, movements);
    }

    [Fact]
    public async Task NegativeAdjustment_CannotDriveStockBelowZero()
    {
        using var harness = new Harness();
        await harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 3 });

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            harness.Service.AdjustAsync(
                harness.PartId,
                new StockAdjustmentRequest { QuantityChange = -4, Reason = "Damaged" }));
    }

    [Fact]
    public async Task Adjustment_WithoutReason_IsRejected()
    {
        using var harness = new Harness();

        await Assert.ThrowsAsync<DomainException>(() =>
            harness.Service.AdjustAsync(
                harness.PartId,
                new StockAdjustmentRequest { QuantityChange = 5, Reason = "  " }));
    }

    [Fact]
    public async Task MovementsAgainstAnInactivePart_AreRejected()
    {
        using var harness = new Harness();
        var part = harness.Db.Parts.First(p => p.Id == harness.PartId);
        part.Deactivate();
        await harness.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 1 }));
    }

    [Fact]
    public async Task MovementsAgainstAMissingPart_AreRejected()
    {
        using var harness = new Harness();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            harness.Service.StockInAsync(Guid.NewGuid(), new StockInRequest { Quantity = 1 }));
    }

    [Fact]
    public async Task MovementWithoutAnAuthenticatedUser_IsRejected()
    {
        using var harness = new Harness();
        harness.Context.CurrentUser.UserId = null;

        await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 1 }));
    }

    [Fact]
    public async Task Notification_CarriesTheResultingQuantityAndLowStockFlag()
    {
        var notifier = new RecordingNotifier();
        using var harness = new Harness(notifier, lowStockThreshold: 10);

        await harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 12 });
        await harness.Service.StockOutAsync(harness.PartId, new StockOutRequest { Quantity = 4 });

        Assert.Equal(2, notifier.Notifications.Count);

        var first = notifier.Notifications[0];
        Assert.Equal(12, first.QuantityOnHand);
        Assert.False(first.IsLowStock);

        var second = notifier.Notifications[1];
        Assert.Equal(8, second.QuantityOnHand);
        Assert.True(second.IsLowStock);
    }

    [Fact]
    public async Task FailedBroadcast_DoesNotRollBackTheMovement()
    {
        using var harness = new Harness(new ThrowingNotifier());

        var movement = await harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 7 });

        Assert.Equal(7, movement.QuantityChange);
        Assert.Equal(1, harness.Db.StockMovements.Count(m => m.PartId == harness.PartId));
    }

    [Fact]
    public async Task MovementHistory_IsReturnedNewestFirst()
    {
        using var harness = new Harness();

        await harness.Service.StockInAsync(harness.PartId, new StockInRequest { Quantity = 10, Reason = "Opening" });
        await harness.Service.StockOutAsync(harness.PartId, new StockOutRequest { Quantity = 2, Reason = "Issue" });

        var history = await harness.Service.SearchAsync(new StockMovementQuery { PartId = harness.PartId });

        Assert.Equal(2, history.TotalCount);
        Assert.Equal(MovementType.Out, history.Items[0].MovementType);
        Assert.Equal("Test User", history.Items[0].PerformedByUserName);
    }
}
