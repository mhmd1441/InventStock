using InventStock.Application.DTOs.Sales;
using InventStock.Application.Common.Models;

namespace InventStock.Application.Interfaces;

public interface ISaleService
{
    Task<SaleDto> PostAsync(PostSaleRequest request, CancellationToken cancellationToken = default);
    Task<SaleOperationDto> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalePaymentAccountDto>> GetPaymentAccountsAsync(CancellationToken cancellationToken = default);
    Task<SaleDto> GetAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleSummaryDto>> GetRecentAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<SaleLookupDto>> SearchAsync(SaleSearchQuery query, CancellationToken cancellationToken = default);
}
