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
}
