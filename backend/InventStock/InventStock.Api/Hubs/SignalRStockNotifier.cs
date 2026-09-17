using InventStock.Application.DTOs.Stock;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace InventStock.Api.Hubs;

public sealed class SignalRStockNotifier : IStockNotifier
{
    private readonly IHubContext<StockHub, IStockClient> _hub;

    public SignalRStockNotifier(IHubContext<StockHub, IStockClient> hub)
    {
        _hub = hub;
    }

    public Task StockChangedAsync(StockChangedNotification notification, CancellationToken cancellationToken = default) =>
        _hub.Clients.All.StockChanged(notification);
}
