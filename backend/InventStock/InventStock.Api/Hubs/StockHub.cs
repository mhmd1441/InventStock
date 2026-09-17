using InventStock.Application.Common;
using InventStock.Application.DTOs.Stock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InventStock.Api.Hubs;

public interface IStockClient
{
    Task StockChanged(StockChangedNotification notification);

    Task GlobalRefreshRequested();
}

/// <summary>
/// Authorisation is declared per method, not only on the hub. A connection-level [Authorize] alone
/// would let any signed-in member of staff invoke administrator-only hub methods.
/// </summary>
[Authorize]
public sealed class StockHub : Hub<IStockClient>
{
    public const string HubPath = "/hubs/stock";

    [Authorize(Roles = RoleNames.Admin)]
    public Task RequestGlobalRefresh() => Clients.All.GlobalRefreshRequested();
}
