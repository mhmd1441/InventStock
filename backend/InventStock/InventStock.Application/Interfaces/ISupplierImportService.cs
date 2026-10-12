using InventStock.Application.DTOs.Imports;

namespace InventStock.Application.Interfaces;

public interface ISupplierImportService
{
    Task<SupplierImportBatchDto> StageAsync(StageSupplierImportRequest request, CancellationToken cancellationToken = default);
    Task<SupplierImportBatchDto> GetAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<SupplierImportBatchDto> ReviewRowAsync(Guid batchId, Guid rowId, ReviewSupplierImportRowRequest request, CancellationToken cancellationToken = default);
    Task<CommitSupplierImportResult> CommitAsync(Guid batchId, CancellationToken cancellationToken = default);
}
