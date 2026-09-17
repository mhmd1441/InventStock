using InventStock.Application.DTOs.Stock;

namespace InventStock.Application.Interfaces;

/// <summary>Transport-agnostic outbound channel for live stock updates.</summary>
public interface IStockNotifier
{
    Task StockChangedAsync(StockChangedNotification notification, CancellationToken cancellationToken = default);
}
