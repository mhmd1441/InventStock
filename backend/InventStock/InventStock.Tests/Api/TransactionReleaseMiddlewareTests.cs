using InventStock.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace InventStock.Tests.Api;

public sealed class TransactionReleaseMiddlewareTests
{
    [Theory]
    [InlineData("/api/sales")]
    [InlineData("/api/sales/operations/123")]
    [InlineData("/api/sale-returns")]
    [InlineData("/api/sale-exchanges")]
    [InlineData("/api/customers/123/payments")]
    [InlineData("/api/admin/cash")]
    [InlineData("/api/pricing/suggestions")]
    [InlineData("/api/admin/pricing/policy")]
    [InlineData("/api/admin/transaction-exceptions")]
    [InlineData("/api/parts/845f5957-d848-4bf8-ae8a-a745939554bd/stock/in")]
    [InlineData("/api/parts/845f5957-d848-4bf8-ae8a-a745939554bd/stock/out")]
    public async Task Default_off_blocks_transaction_routes(string path)
    {
        var nextCalled = false;
        var middleware = new TransactionReleaseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Disabled_gate_keeps_catalogue_available()
    {
        var nextCalled = false;
        var middleware = new TransactionReleaseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/parts";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Disabled_gate_keeps_physical_counts_available()
    {
        var nextCalled = false;
        var middleware = new TransactionReleaseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/parts/845f5957-d848-4bf8-ae8a-a745939554bd/stock/count";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Explicit_enable_allows_transaction_routes()
    {
        var nextCalled = false;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TransactionRelease:Enabled"] = "true"
        }).Build();
        var middleware = new TransactionReleaseMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, configuration);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/sales";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
