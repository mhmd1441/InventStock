using InventStock.Application.Common.Models;
using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;

namespace InventStock.Application.Interfaces.Repositories;

public interface IPartRepository
{
    Task<PagedResult<CounterPartDto>> SearchCounterAsync(PartQuery query, CancellationToken cancellationToken = default);

    Task<CounterPartDto?> GetCounterAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CounterPartDto?> GetCounterByNumberAsync(string number, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CounterPartDto>> GetCounterLowStockAsync(int limit, CancellationToken cancellationToken = default);

    Task<Part?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PartDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PartDto?> GetDtoByPartNumberAsync(string partNumber, CancellationToken cancellationToken = default);

    Task<PagedResult<PartDto>> SearchAsync(PartQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartDto>> GetLowStockAsync(int limit, CancellationToken cancellationToken = default);

    Task<bool> PartNumberExistsAsync(string partNumber, Guid? excludeId, CancellationToken cancellationToken = default, string brand = "", string variant = "");

    Task<bool> BarcodeExistsAsync(string barcode, Guid? excludeId, CancellationToken cancellationToken = default);

    Task<int> GetQuantityOnHandAsync(Guid partId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a row-level write lock on the part so concurrent stock movements are serialised
    /// and the read-then-write check for available stock cannot race.
    /// </summary>
    Task LockForStockChangeAsync(Guid partId, CancellationToken cancellationToken = default);

    Task AddAsync(Part part, CancellationToken cancellationToken = default);
}
