using InventStock.Application.DTOs.Sales;

namespace InventStock.Application.Interfaces;

public interface ISaleReturnService
{
    Task<SaleReturnDto> PostAsync(PostSaleReturnRequest request, CancellationToken ct = default);
    Task<SaleReturnDto> GetOperationAsync(Guid operationId, CancellationToken ct = default);
    Task<ReturnableSaleDto> GetAvailableAsync(Guid saleId, CancellationToken ct = default);
    Task<LegacySaleReturnDto> PostLegacyAsync(PostLegacySaleReturnRequest request, CancellationToken ct = default);
    Task<LegacySaleReturnDto> GetLegacyOperationAsync(Guid operationId, CancellationToken ct = default);
}
