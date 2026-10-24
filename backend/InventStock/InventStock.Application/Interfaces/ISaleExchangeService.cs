using InventStock.Application.DTOs.Sales;

namespace InventStock.Application.Interfaces;

public interface ISaleExchangeService
{
    Task<SaleExchangeDto> PostAsync(PostSaleExchangeRequest request, CancellationToken ct = default);
    Task<SaleExchangeDto> GetOperationAsync(Guid operationId, CancellationToken ct = default);
}
