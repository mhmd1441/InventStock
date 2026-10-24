using System.Net;
using System.Text;

namespace InventStock.Tests.Api;

/// <summary>
/// Proves the release gate at the authenticated HTTP boundary, not just as an isolated middleware.
/// Factories are deliberately used one after the other because the application reads the gate from
/// process environment while the test host is composed.
/// </summary>
public sealed class TransactionReleaseApiTests
{
    private static readonly string[] StaffAndAdminGetPaths =
    [
        "/api/sales/payment-accounts",
        "/api/pricing/suggestions"
    ];

    [Fact]
    public async Task Disabled_gate_hides_all_transaction_families_from_admin_and_staff()
    {
        await using var factory = new ApiFactory(transactionReleaseEnabled: false);
        await factory.InitializeAsync();
        using var admin = await factory.CreateAdminClientAsync();
        using var staff = await factory.CreateStaffClientAsync();

        foreach (var client in new[] { admin, staff })
        {
            foreach (var path in StaffAndAdminGetPaths)
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);

            using var customerBody = new StringContent("{}", Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsync("/api/customers", customerBody)).StatusCode);

            using var returnBody = new StringContent("{}", Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.NotFound,
                (await client.PostAsync("/api/sale-returns", returnBody)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/admin/cash/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staff.GetAsync("/api/admin/cash/accounts")).StatusCode);
    }

    [Fact]
    public async Task Enabled_gate_exposes_routes_with_normal_role_authorization()
    {
        await using var factory = new ApiFactory(transactionReleaseEnabled: true);
        await factory.InitializeAsync();
        using var admin = await factory.CreateAdminClientAsync();
        using var staff = await factory.CreateStaffClientAsync();

        foreach (var client in new[] { admin, staff })
        {
            foreach (var path in StaffAndAdminGetPaths)
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);

            using var customerBody = new StringContent("{}", Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsync("/api/customers", customerBody)).StatusCode);

            using var returnBody = new StringContent("{}", Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsync("/api/sale-returns", returnBody)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK,
            (await admin.GetAsync("/api/admin/cash/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staff.GetAsync("/api/admin/cash/accounts")).StatusCode);
    }
}
