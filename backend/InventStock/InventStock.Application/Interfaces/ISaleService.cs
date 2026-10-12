using InventStock.Application.DTOs.Sales;

namespace InventStock.Application.Interfaces;

public interface ISaleService
{
    Task<SaleDto> PostAsync(PostSaleRequest request, CancellationToken cancellationToken = default);
    Task<SaleOperationDto> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalePaymentAccountDto>> GetPaymentAccountsAsync(CancellationToken cancellationToken = default);
}
