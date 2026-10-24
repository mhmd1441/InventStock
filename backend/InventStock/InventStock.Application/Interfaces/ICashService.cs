using InventStock.Application.DTOs.Cash;

namespace InventStock.Application.Interfaces;

public interface ICashService
{
    Task<IReadOnlyList<CashAccountDto>> ListAccountsAsync(CancellationToken cancellationToken = default);
    Task<CashOpeningBalanceDto> SetOpeningBalanceAsync(Guid accountId, PostCashOpeningBalanceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseCategoryDto>> ListCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<ExpenseCategoryDto> CreateCategoryAsync(SaveExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task<ExpenseCategoryDto> RenameCategoryAsync(Guid id, SaveExpenseCategoryRequest request, CancellationToken cancellationToken = default);
    Task SetCategoryActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<OutgoingPaymentDto> PostOutgoingAsync(PostOutgoingPaymentRequest request, CancellationToken cancellationToken = default);
    Task<OutgoingPaymentDto> ReverseOutgoingAsync(Guid paymentId, ReverseOutgoingPaymentRequest request, CancellationToken cancellationToken = default);
    Task<OutgoingPaymentListDto> ListOutgoingsAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<CashReconciliationDayDto> GetReconciliationDayAsync(DateOnly businessDate, CancellationToken cancellationToken = default);
    Task<CashReconciliationDto> ReconcileAsync(Guid accountId, PostCashReconciliationRequest request,
        CancellationToken cancellationToken = default);
    Task<CashReconciliationCorrectionDto> CorrectReconciliationAsync(Guid reconciliationId,
        PostCashReconciliationCorrectionRequest request, CancellationToken cancellationToken = default);
    Task<SupplierPaymentDto> PostSupplierPaymentAsync(PostSupplierPaymentRequest request, CancellationToken cancellationToken = default);
    Task<SupplierPaymentDto> ReverseSupplierPaymentAsync(Guid paymentId, ReverseSupplierPaymentRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierPaymentDto>> ListSupplierPaymentsAsync(int year, int month, CancellationToken cancellationToken = default);
    Task<CashTransferDto> PostTransferAsync(PostCashTransferRequest request, CancellationToken cancellationToken = default);
    Task<CashTransferDto> ReverseTransferAsync(Guid transferId, ReverseCashTransferRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashTransferDto>> ListTransfersAsync(int year, int month, CancellationToken cancellationToken = default);
}
