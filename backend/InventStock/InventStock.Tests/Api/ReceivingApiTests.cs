using System.Net;
using System.Net.Http.Json;
using InventStock.Application.DTOs.Receiving;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Tests.Api;

public sealed class ReceivingApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Admin_receipt_atomically_adds_stock_and_actual_cost_and_replays_safely()
    {
        Guid supplierId;
        Guid[] partIds;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            supplierId = await db.Suppliers.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
            partIds = await db.Parts.Where(x => x.IsActive).OrderBy(x => x.Id).Select(x => x.Id).Take(2).ToArrayAsync();
        }

        var operationId = Guid.NewGuid();
        var request = new PostPurchaseReceiptRequest
        {
            OperationId = operationId,
            SupplierId = supplierId,
            SupplierReference = "INV-RECEIVE-1",
            ReceivedAtUtc = DateTime.UtcNow,
            Lines =
            [
                new() { PartId = partIds[0], PackageCount = 2, BaseUnitsPerPackage = 10,
                    CostPerPackage = 15m, Currency = MoneyCurrency.USD, TaxTreatment = TaxTreatment.Excluded },
                new() { PartId = partIds[1], PackageCount = 3, BaseUnitsPerPackage = 1,
                    CostPerPackage = null, Currency = null, TaxTreatment = TaxTreatment.Unknown }
            ]
        };

        using var client = await factory.CreateAdminClientAsync();
        using var firstResponse = await client.PostAsJsonAsync("/api/admin/receipts", request);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<PurchaseReceiptDto>();
        Assert.NotNull(first);
        Assert.Equal(2, first.Lines.Count);
        Assert.Equal(20, first.Lines.Single(x => x.PartId == partIds[0]).BaseQuantity);
        Assert.NotNull(first.Lines.Single(x => x.PartId == partIds[0]).SupplierCostObservationId);
        Assert.Null(first.Lines.Single(x => x.PartId == partIds[1]).SupplierCostObservationId);

        using var replayResponse = await client.PostAsJsonAsync("/api/admin/receipts", request);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        var replay = await replayResponse.Content.ReadFromJsonAsync<PurchaseReceiptDto>();
        Assert.Equal(first.Id, replay!.Id);

        using var scope2 = factory.Services.CreateScope();
        var verify = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await verify.PurchaseReceipts.CountAsync(x => x.OperationId == operationId));
        Assert.Equal(2, await verify.PurchaseReceiptLines.CountAsync(x => x.PurchaseReceiptId == first.Id));
        Assert.Equal(2, await verify.StockMovements.CountAsync(x => x.PurchaseReceiptLineId != null &&
            first.Lines.Select(l => l.Id).Contains(x.PurchaseReceiptLineId.Value)));
        Assert.Equal(1, await verify.SupplierCostObservations.CountAsync(x => x.PurchaseReceiptLineId != null &&
            first.Lines.Select(l => l.Id).Contains(x.PurchaseReceiptLineId.Value)));
    }

    [Fact]
    public async Task Receipt_is_admin_only_and_changed_replay_conflicts()
    {
        Guid supplierId;
        Guid partId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            supplierId = await db.Suppliers.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
            partId = await db.Parts.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
        }

        var request = new PostPurchaseReceiptRequest
        {
            OperationId = Guid.NewGuid(), SupplierId = supplierId, ReceivedAtUtc = DateTime.UtcNow,
            Lines = [new() { PartId = partId, PackageCount = 1, BaseUnitsPerPackage = 1 }]
        };

        using var staff = await factory.CreateStaffClientAsync();
        using var forbidden = await staff.PostAsJsonAsync("/api/admin/receipts", request);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var admin = await factory.CreateAdminClientAsync();
        using var created = await admin.PostAsJsonAsync("/api/admin/receipts", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var changed = request with { Notes = "different command" };
        using var conflict = await admin.PostAsJsonAsync("/api/admin/receipts", changed);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
}
