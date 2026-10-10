using InventStock.Domain.Entities;

namespace InventStock.Application.Interfaces;

public sealed record ReferenceDto(Guid Id, ReferenceKind Kind, string Issuer, string Value, bool IsActive = true, int Revision = 0);
public sealed record AddReferenceRequest(ReferenceKind Kind, string? Issuer, string Value);
public sealed record SetReferenceStateRequest(bool IsActive, string Reason, int ExpectedRevision);
public sealed record ReferenceEventDto(int Revision, bool IsActive, string Reason, Guid ActorId, DateTime OccurredAtUtc);

public interface IReferenceService
{
    Task<IReadOnlyList<ReferenceDto>> ListAsync(Guid partId, CancellationToken cancellationToken = default);
    Task<ReferenceDto> AddAsync(Guid partId, AddReferenceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceDto>> ListAllAsync(Guid partId, CancellationToken cancellationToken = default);
    Task SetStateAsync(Guid partId, Guid referenceId, SetReferenceStateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReferenceEventDto>> HistoryAsync(Guid partId, Guid referenceId, CancellationToken cancellationToken = default);
}
