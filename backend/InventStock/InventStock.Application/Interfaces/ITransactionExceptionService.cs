using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Exceptions;

namespace InventStock.Application.Interfaces;

public interface ITransactionExceptionService
{
    Task<PagedResult<TransactionExceptionDto>> ListAsync(TransactionExceptionQuery query,
        CancellationToken ct = default);
}
