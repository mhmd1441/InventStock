using InventStock.Application.Common.Exceptions;
using InventStock.Application.Interfaces;
using InventStock.Domain.Entities;
using InventStock.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InventStock.Infrastructure.Services;

public sealed class ReferenceService(AppDbContext db, ICurrentUserService user) : IReferenceService
{
    public Task<IReadOnlyList<ReferenceDto>> ListAsync(Guid partId, CancellationToken cancellationToken = default) => List(partId, false, cancellationToken);
    public Task<IReadOnlyList<ReferenceDto>> ListAllAsync(Guid partId, CancellationToken cancellationToken = default) => List(partId, true, cancellationToken);
    private async Task<IReadOnlyList<ReferenceDto>> List(Guid partId, bool includeInactive, CancellationToken cancellationToken)
    {
        if (!await db.Parts.AnyAsync(p => p.Id == partId, cancellationToken)) throw new NotFoundException("Part", partId);
        return await db.Set<SkuReference>().AsNoTracking().Where(x => x.PartId == partId && (includeInactive || x.IsActive))
            .OrderBy(x => x.Reference.Kind).ThenBy(x => x.Reference.Value)
            .Select(x => new ReferenceDto(x.ReferenceId, x.Reference.Kind, x.Reference.Issuer, x.Reference.Value, x.IsActive, x.Revision))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReferenceDto> AddAsync(Guid partId, AddReferenceRequest request, CancellationToken cancellationToken = default)
    {
        var candidate = CatalogueReference.Create(request.Kind, request.Issuer, request.Value);
        var actor = user.UserId ?? throw new UnauthorizedAccessException();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!await db.Parts.AnyAsync(p => p.Id == partId, cancellationToken)) throw new NotFoundException("Part", partId);
        var reference = await db.Set<CatalogueReference>().SingleOrDefaultAsync(x =>
            x.Kind == candidate.Kind && x.Issuer == candidate.Issuer && x.IdentityKey == candidate.IdentityKey, cancellationToken);
        reference ??= candidate;
        if (reference == candidate) db.Add(reference);
        var link = await db.Set<SkuReference>().SingleOrDefaultAsync(x => x.PartId == partId && x.ReferenceId == reference.Id, cancellationToken);
        if (link is { IsActive: false }) throw new ConflictException("This link was deactivated. Restore it with a reason instead of adding it again.");
        if (link == null)
        {
            link = new SkuReference { PartId = partId, Reference = reference, ReferenceId = reference.Id,
                LinkedAtUtc = DateTime.UtcNow, LinkedByUserId = actor, Revision = 1 };
            db.Add(link);
            db.Add(new ReferenceLinkEvent { PartId = partId, ReferenceId = reference.Id, Revision = 1,
                IsActive = true, Reason = "Reference linked", ActorId = actor, OccurredAtUtc = DateTime.UtcNow });
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("This reference was linked by another request. Refresh and retry.");
        }
        return new(reference.Id, reference.Kind, reference.Issuer, reference.Value, link.IsActive, link.Revision);
    }

    public async Task SetStateAsync(Guid partId, Guid referenceId, SetReferenceStateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = user.UserId ?? throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            throw new ConflictException("A reason of 1–500 characters is required.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var link = await db.Set<SkuReference>().SingleOrDefaultAsync(x => x.PartId == partId && x.ReferenceId == referenceId, cancellationToken)
            ?? throw new NotFoundException("Reference link was not found.");
        if (link.Revision != request.ExpectedRevision) throw new ConflictException("The link changed. Refresh before making another correction.");
        if (link.IsActive == request.IsActive) throw new ConflictException("The link is already in that state.");
        link.IsActive = request.IsActive;
        link.Revision++;
        db.Add(new ReferenceLinkEvent { PartId = partId, ReferenceId = referenceId, Revision = link.Revision,
            IsActive = link.IsActive, Reason = request.Reason.Trim(), ActorId = actor, OccurredAtUtc = DateTime.UtcNow });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("The link changed. Refresh before making another correction."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ConflictException("The link changed. Refresh before making another correction."); }
    }

    public async Task<IReadOnlyList<ReferenceEventDto>> HistoryAsync(Guid partId, Guid referenceId, CancellationToken cancellationToken = default) =>
        await db.Set<ReferenceLinkEvent>().AsNoTracking().Where(x => x.PartId == partId && x.ReferenceId == referenceId)
            .OrderBy(x => x.Revision).Select(x => new ReferenceEventDto(x.Revision, x.IsActive, x.Reason, x.ActorId, x.OccurredAtUtc))
            .ToListAsync(cancellationToken);
}
