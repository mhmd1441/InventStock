using System.Net;
using System.Net.Http.Json;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InventStock.Tests.Api;

[Collection("PostgreSQL schema migrations")]
public sealed class PricingApiTests(PostgresSaleApiFactory factory) : IClassFixture<PostgresSaleApiFactory>
{
    [Fact]
    public async Task Direct_cut_hose_rate_sells_half_a_metre_for_three_dollars_and_remains_audited()
    {
        Guid partId, accountId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.FirstAsync();
            var supplier = await db.Suppliers.FirstAsync();
            var actor = await db.Users.Where(x => x.Email == ApiFactory.StaffEmail).Select(x => x.Id).SingleAsync();
            var part = Part.Create("CUT-" + Guid.NewGuid().ToString("N"), "Cut hose", null,
                category.Id, null, 0m, 0, null);
            part.SetStockUnit(StockUnit.Centimeter, false);
            partId = part.Id;
            db.Parts.Add(part);
            db.StockMovements.Add(StockMovement.PhysicalCount(partId, 100, 0, actor, "Test metre count"));
            // An unrelated EUR purchase with excluded TVA must not influence a directly set hose selling rate.
            db.SupplierCostObservations.Add(SupplierCostObservation.Create(partId, supplier.Id, 100m,
                MoneyCurrency.EUR, TaxTreatment.Excluded, CostSource.ActualPurchase, 100,
                "PRIVATE", null, DateTime.UtcNow.AddDays(-1)));
            await db.SaveChangesAsync();
            accountId = await db.CashAccounts.Where(x => x.Code == "SAFE_USD").Select(x => x.Id).SingleAsync();
        }
        using var staff = await factory.CreateStaffClientAsync();
        using var admin = await factory.CreateAdminClientAsync();
        using var forbidden = await staff.GetAsync($"/api/admin/pricing/cut-hose/{partId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var initial = await admin.GetFromJsonAsync<CutHoseSellingRateDto>($"/api/admin/pricing/cut-hose/{partId}");
        Assert.Null(initial!.UsdPerMeter);
        using var savedResponse = await admin.PutAsJsonAsync($"/api/admin/pricing/cut-hose/{partId}",
            new SetCutHoseSellingRateRequest(null, 6m));
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = await savedResponse.Content.ReadFromJsonAsync<CutHoseSellingRateDto>();
        Assert.Equal(6m, saved!.UsdPerMeter);
        using var publicResponse = await staff.GetAsync($"/api/pricing/suggestions?partIds={partId}");
        var publicJson = await publicResponse.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "cost", "supplier", "PRIVATE", "EUR", "taxTreatment" })
            Assert.DoesNotContain(secret, publicJson, StringComparison.OrdinalIgnoreCase);
        var suggestions = await publicResponse.Content.ReadFromJsonAsync<List<SellingPriceSuggestionDto>>();
        Assert.Equal(0.06m, suggestions!.Single().SuggestedUsd); // $6 per metre / 100 cm.

        var request = new PostSaleRequest { OperationId = Guid.NewGuid(), ConfirmedTotalUsd = 3m,
            Lines = [new SaleLineRequest(partId, 50, 0.06m)],
            Payments = [new SaleTenderRequest(accountId, MoneyCurrency.USD, 3m, 3m)] };
        using var posted = await staff.PostAsJsonAsync("/api/sales", request);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
        var sale = await posted.Content.ReadFromJsonAsync<SaleDto>();
        Assert.Equal(3m, sale!.TotalUsd);
        Assert.Equal(50, sale.Lines.Single().Quantity);
        Assert.Equal(0.06m, sale.Lines.Single().SuggestedUnitPriceUsd);
        Assert.False(sale.Lines.Single().PriceOverridden);

        using var stale = await admin.PutAsJsonAsync($"/api/admin/pricing/cut-hose/{partId}",
            new SetCutHoseSellingRateRequest(null, 7m));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var changed = await admin.PutAsJsonAsync($"/api/admin/pricing/cut-hose/{partId}",
            new SetCutHoseSellingRateRequest(saved.CurrentRateId, 8m));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var changedRate = await changed.Content.ReadFromJsonAsync<CutHoseSellingRateDto>();
        using var cleared = await admin.PutAsJsonAsync($"/api/admin/pricing/cut-hose/{partId}",
            new SetCutHoseSellingRateRequest(changedRate!.CurrentRateId, null));
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        using var noSuggestion = await staff.GetAsync($"/api/pricing/suggestions?partIds={partId}");
        Assert.Null((await noSuggestion.Content.ReadFromJsonAsync<List<SellingPriceSuggestionDto>>())!.Single().SuggestedUsd);
        using var replay = await staff.PostAsJsonAsync("/api/sales", request);
        var old = await replay.Content.ReadFromJsonAsync<SaleDto>();
        Assert.Equal(sale.Id, old!.Id);
        Assert.Equal(0.06m, old.Lines.Single().SuggestedUnitPriceUsd);
        using var scope2 = factory.Services.CreateScope();
        var verify = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await verify.CutHoseSellingRates.CountAsync(x => x.PartId == partId));
        Assert.Equal(0.06m, (await verify.SaleLines.SingleAsync(x => x.SaleId == sale.Id)).SuggestedUnitPriceUsd);
    }

    [Fact]
    public async Task Posted_sale_keeps_the_server_suggestion_and_below_suggestion_override_after_policy_changes()
    {
        Guid partId, accountId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.FirstAsync();
            var supplier = await db.Suppliers.FirstAsync();
            var actor = await db.Users.Where(x => x.Email == ApiFactory.StaffEmail).Select(x => x.Id).SingleAsync();
            var part = Part.Create("OVERRIDE-" + Guid.NewGuid().ToString("N"), "Override hose", null,
                category.Id, null, 0m, 0, null);
            partId = part.Id;
            db.Parts.Add(part);
            db.StockMovements.Add(StockMovement.PhysicalCount(partId, 2, 0, actor, "Test count"));
            db.SupplierCostObservations.Add(SupplierCostObservation.Create(partId, supplier.Id, 15m,
                MoneyCurrency.USD, TaxTreatment.Excluded, CostSource.ActualPurchase, 1,
                "PRIVATE price", null, DateTime.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();
            accountId = await db.CashAccounts.Where(x => x.Code == "SAFE_USD").Select(x => x.Id).SingleAsync();
        }
        using var staff = await factory.CreateStaffClientAsync();
        var request = new PostSaleRequest { OperationId = Guid.NewGuid(), ConfirmedTotalUsd = 18m,
            Lines = [new SaleLineRequest(partId, 1, 18m)],
            Payments = [new SaleTenderRequest(accountId, MoneyCurrency.USD, 18m, 18m)] };
        using var posted = await staff.PostAsJsonAsync("/api/sales", request);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
        var sale = await posted.Content.ReadFromJsonAsync<SaleDto>();
        Assert.NotNull(sale);
        Assert.Equal(20m, sale.Lines.Single().SuggestedUnitPriceUsd);
        Assert.True(sale.Lines.Single().PriceOverridden);
        Assert.True(sale.Lines.Single().UnitSellingPriceUsd < sale.Lines.Single().SuggestedUnitPriceUsd);
        foreach (var secret in new[] { "cost", "supplier", "PRIVATE" })
            Assert.DoesNotContain(secret, await posted.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var admin = await factory.CreateAdminClientAsync();
        var policy = await admin.GetFromJsonAsync<PricingPolicyDto>("/api/admin/pricing/policy");
        using var changed = await admin.PutAsJsonAsync("/api/admin/pricing/policy",
            new UpdatePricingPolicyRequest(50m, policy!.TvaPercent, null, null, policy.Revision));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var replay = await staff.PostAsJsonAsync("/api/sales", request);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var replayed = await replay.Content.ReadFromJsonAsync<SaleDto>();
        Assert.Equal(sale.Id, replayed!.Id);
        Assert.Equal(20m, replayed.Lines.Single().SuggestedUnitPriceUsd);
        using var returnLookup = await staff.GetAsync($"/api/sale-returns/sales/{sale.Id}/availability");
        Assert.Equal(HttpStatusCode.OK, returnLookup.StatusCode);
        Assert.DoesNotContain("cost", await returnLookup.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var scope2 = factory.Services.CreateScope();
        var verify = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await verify.SaleLines.SingleAsync(x => x.SaleId == sale.Id);
        Assert.Equal(20m, persisted.SuggestedUnitPriceUsd);
        Assert.True(persisted.PriceOverridden);
        var current = await admin.GetFromJsonAsync<PricingPolicyDto>("/api/admin/pricing/policy");
        using var restored = await admin.PutAsJsonAsync("/api/admin/pricing/policy",
            new UpdatePricingPolicyRequest(policy.MarkupPercent, policy.TvaPercent,
                policy.EurUsdRate, policy.LbpPerUsdRate, current!.Revision));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task Staff_sees_only_suggestion_from_latest_actual_purchase_and_owner_controls_the_rule()
    {
        Guid usdId, eurId, unknownId, quoteOnlyId, lbpId, cutHoseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var category = await db.Categories.FirstAsync();
            var supplier = await db.Suppliers.FirstAsync();
            var parts = Enumerable.Range(0, 6).Select(i => Part.Create("PRICE-" + Guid.NewGuid().ToString("N"),
                $"Pricing case {i}", null, category.Id, null, 0m, 0, null)).ToArray();
            parts[5].SetStockUnit(StockUnit.Centimeter, false);
            db.Parts.AddRange(parts);
            (usdId, eurId, unknownId, quoteOnlyId) = (parts[0].Id, parts[1].Id, parts[2].Id, parts[3].Id);
            lbpId = parts[4].Id;
            cutHoseId = parts[5].Id;
            var now = DateTime.UtcNow;
            db.SupplierCostObservations.AddRange(
                SupplierCostObservation.Create(usdId, supplier.Id, 15m, MoneyCurrency.USD, TaxTreatment.Excluded,
                    CostSource.ActualPurchase, 1, "PRIVATE-USD", null, now.AddDays(-2)),
                SupplierCostObservation.Create(usdId, supplier.Id, 2m, MoneyCurrency.USD, TaxTreatment.Included,
                    CostSource.SupplierQuote, 1, "PRIVATE-QUOTE", null, now),
                SupplierCostObservation.Create(eurId, supplier.Id, 30m, MoneyCurrency.EUR, TaxTreatment.Included,
                    CostSource.ActualPurchase, 2, "PRIVATE-EUR", null, now.AddDays(-1)),
                SupplierCostObservation.Create(unknownId, supplier.Id, 10m, MoneyCurrency.USD, TaxTreatment.Unknown,
                    CostSource.ActualPurchase, 1, null, null, now.AddDays(-1)),
                SupplierCostObservation.Create(quoteOnlyId, supplier.Id, 10m, MoneyCurrency.USD, TaxTreatment.Included,
                    CostSource.SupplierQuote, 1, null, null, now.AddDays(-1)),
                SupplierCostObservation.Create(lbpId, supplier.Id, 900000m, MoneyCurrency.LBP, TaxTreatment.Included,
                    CostSource.ActualPurchase, 1, null, null, now.AddDays(-1)),
                SupplierCostObservation.Create(cutHoseId, supplier.Id, 13m, MoneyCurrency.USD, TaxTreatment.Included,
                    CostSource.ActualPurchase, 100, null, null, now.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        var query = $"?partIds={usdId}&partIds={eurId}&partIds={unknownId}&partIds={quoteOnlyId}&partIds={lbpId}&partIds={cutHoseId}";
        using var staff = await factory.CreateStaffClientAsync();
        using var first = await staff.GetAsync("/api/pricing/suggestions" + query);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var publicJson = await first.Content.ReadAsStringAsync();
        foreach (var secret in new[] { "cost", "supplier", "currency", "taxTreatment", "PRIVATE" })
            Assert.DoesNotContain(secret, publicJson, StringComparison.OrdinalIgnoreCase);
        var suggestions = await first.Content.ReadFromJsonAsync<List<SellingPriceSuggestionDto>>();
        Assert.NotNull(suggestions);
        Assert.Equal(20m, suggestions.Single(x => x.PartId == usdId).SuggestedUsd);
        Assert.Null(suggestions.Single(x => x.PartId == eurId).SuggestedUsd);
        Assert.Null(suggestions.Single(x => x.PartId == unknownId).SuggestedUsd);
        Assert.Null(suggestions.Single(x => x.PartId == quoteOnlyId).SuggestedUsd);
        Assert.Null(suggestions.Single(x => x.PartId == lbpId).SuggestedUsd);
        Assert.Null(suggestions.Single(x => x.PartId == cutHoseId).SuggestedUsd);
        using var denied = await staff.GetAsync("/api/admin/pricing/policy");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var deniedCalculation = await staff.GetAsync("/api/admin/pricing/calculations" + query);
        Assert.Equal(HttpStatusCode.Forbidden, deniedCalculation.StatusCode);

        using var admin = await factory.CreateAdminClientAsync();
        var policy = await admin.GetFromJsonAsync<PricingPolicyDto>("/api/admin/pricing/policy");
        using var updated = await admin.PutAsJsonAsync("/api/admin/pricing/policy",
            new UpdatePricingPolicyRequest(20m, 11m, 1.17m, 90000m, policy!.Revision));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var second = await staff.GetAsync("/api/pricing/suggestions" + query);
        var changed = await second.Content.ReadFromJsonAsync<List<SellingPriceSuggestionDto>>();
        Assert.NotNull(changed);
        Assert.Equal(20m, changed.Single(x => x.PartId == usdId).SuggestedUsd);
        Assert.Equal(22m, changed.Single(x => x.PartId == eurId).SuggestedUsd);
        Assert.Equal(12m, changed.Single(x => x.PartId == lbpId).SuggestedUsd);
        using var stale = await admin.PutAsJsonAsync("/api/admin/pricing/policy",
            new UpdatePricingPolicyRequest(50m, 11m, 1.17m, null, policy.Revision));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var privateResponse = await admin.GetAsync("/api/admin/pricing/calculations" + query);
        Assert.Equal(HttpStatusCode.OK, privateResponse.StatusCode);
        var privateRows = await privateResponse.Content.ReadFromJsonAsync<List<AdminPriceCalculationDto>>();
        Assert.Equal(15m, privateRows!.Single(x => x.PartId == usdId).LatestActualPackageCost);
        var current = await admin.GetFromJsonAsync<PricingPolicyDto>("/api/admin/pricing/policy");
        using var restored = await admin.PutAsJsonAsync("/api/admin/pricing/policy",
            new UpdatePricingPolicyRequest(policy.MarkupPercent, policy.TvaPercent,
                policy.EurUsdRate, policy.LbpPerUsdRate, current!.Revision));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }
}
