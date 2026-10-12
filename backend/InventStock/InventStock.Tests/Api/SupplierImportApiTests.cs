using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using InventStock.Application.DTOs.Imports;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Tests.Api;

public sealed class SupplierImportApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Reviewed_import_commits_catalogue_and_costs_without_creating_stock()
    {
        Guid supplierId;
        Guid categoryId;
        Guid existingPartId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            supplierId = await db.Suppliers.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
            categoryId = await db.Categories.Select(x => x.Id).FirstAsync();
            existingPartId = await db.Parts.Where(x => x.IsActive).Select(x => x.Id).FirstAsync();
        }

        var hash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var uniqueNumber = "IMP" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var stageRequest = new StageSupplierImportRequest
        {
            SupplierId = supplierId, SourceFileName = "supplier-list.pdf", SourceSha256 = hash,
            Rows =
            [
                new() { SourceRowNumber = 1, PartNumber = uniqueNumber, Name = "Imported hose",
                    Brand = "Matrix", CatalogueCost = 12.5m, Currency = MoneyCurrency.USD,
                    TaxTreatment = TaxTreatment.Excluded, SupplierCode = "MX-1" },
                new() { SourceRowNumber = 2, PartNumber = "LINK-ME", Name = "Existing offer",
                    CatalogueCost = 10m, Currency = MoneyCurrency.USD },
                new() { SourceRowNumber = 3, PartNumber = "SKIP-ME", Name = "Not relevant" }
            ]
        };

        using var admin = await factory.CreateAdminClientAsync();
        using var stagedResponse = await admin.PostAsJsonAsync("/api/admin/supplier-imports", stageRequest);
        Assert.Equal(HttpStatusCode.Created, stagedResponse.StatusCode);
        var batch = await stagedResponse.Content.ReadFromJsonAsync<SupplierImportBatchDto>();
        Assert.NotNull(batch);

        using var duplicate = await admin.PostAsJsonAsync("/api/admin/supplier-imports", stageRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        batch = await Review(admin, batch.Id, batch.Rows[0].Id, new()
        {
            Decision = SupplierImportDecision.CreateNew, CategoryId = categoryId,
            BrandId = ProductBrand.UnknownId, StockUnit = StockUnit.Piece
        });
        batch = await Review(admin, batch.Id, batch.Rows[1].Id, new()
        {
            Decision = SupplierImportDecision.LinkExisting, TargetPartId = existingPartId
        });
        batch = await Review(admin, batch.Id, batch.Rows[2].Id, new()
        {
            Decision = SupplierImportDecision.Skip
        });

        using var commitResponse = await admin.PostAsync($"/api/admin/supplier-imports/{batch.Id}/commit", null);
        Assert.Equal(HttpStatusCode.OK, commitResponse.StatusCode);
        var result = await commitResponse.Content.ReadFromJsonAsync<CommitSupplierImportResult>();
        Assert.NotNull(result);
        Assert.Equal(1, result.CreatedParts);
        Assert.Equal(1, result.LinkedParts);
        Assert.Equal(1, result.SkippedRows);
        Assert.Equal(2, result.CostObservations);

        using var verifyScope = factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var createdPart = await verify.Parts.SingleAsync(x => x.PartNumber == uniqueNumber);
        Assert.False(await verify.StockMovements.AnyAsync(x => x.PartId == createdPart.Id));
        Assert.Equal(2, await verify.SupplierCostObservations.CountAsync(x => result.ResultPartIds.Contains(x.PartId) && x.Source == CostSource.SupplierCatalogue));
        Assert.Equal(SupplierImportStatus.Committed,
            await verify.SupplierImportBatches.Where(x => x.Id == batch.Id).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Supplier_import_routes_are_admin_only()
    {
        using var staff = await factory.CreateStaffClientAsync();
        using var response = await staff.PostAsJsonAsync("/api/admin/supplier-imports", new StageSupplierImportRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<SupplierImportBatchDto> Review(HttpClient client, Guid batchId, Guid rowId,
        ReviewSupplierImportRowRequest request)
    {
        using var response = await client.PutAsJsonAsync($"/api/admin/supplier-imports/{batchId}/rows/{rowId}/review", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupplierImportBatchDto>())!;
    }
}
