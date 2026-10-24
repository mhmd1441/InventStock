namespace InventStock.Api.Middleware;

/// <summary>Fail-closed release gate for unfinished transaction workflows.</summary>
public sealed class TransactionReleaseMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private static readonly PathString[] ProtectedPaths =
    [
        new("/api/sales"),
        new("/api/sale-returns"),
        new("/api/sale-exchanges"),
        new("/api/customers"),
        new("/api/admin/cash"),
        new("/api/pricing"),
        new("/api/admin/pricing"),
        new("/api/admin/transaction-exceptions")
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (configuration.GetValue<bool>("TransactionRelease:Enabled") ||
            !IsProtected(context.Request.Path))
        {
            await next(context);
            return;
        }

        // Hide unfinished transaction surfaces rather than advertising that they exist.
        // Normal endpoint authorization still runs first, so callers without the endpoint's
        // role may receive 403 before this gate is reached.
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Transaction workflows are not enabled for this installation."
        });
    }

    private static bool IsProtected(PathString requestPath)
    {
        if (ProtectedPaths.Any(path => requestPath.StartsWithSegments(path))) return true;

        // Raw stock-in/out can otherwise become an unofficial receiving/sale workflow while the
        // corresponding money and audit modules are deliberately disabled. Counts remain usable.
        var value = requestPath.Value;
        return value is not null && value.StartsWith("/api/parts/", StringComparison.OrdinalIgnoreCase) &&
            (value.EndsWith("/stock/in", StringComparison.OrdinalIgnoreCase) ||
             value.EndsWith("/stock/out", StringComparison.OrdinalIgnoreCase));
    }
}
