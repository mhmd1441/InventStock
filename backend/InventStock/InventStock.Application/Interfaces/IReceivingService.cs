using InventStock.Application.DTOs.Receiving;

namespace InventStock.Application.Interfaces;

public interface IReceivingService
{
    Task<PurchaseReceiptDto> PostAsync(PostPurchaseReceiptRequest request, CancellationToken cancellationToken = default);
    Task<ReceivingOperationStatusDto> GetOperationStatusAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task<PurchaseReceiptDto> GetAsync(Guid receiptId, CancellationToken cancellationToken = default);
}
