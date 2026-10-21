using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Mapping;
using RequestsManagement.Models;
using RequestsManagement.Policies;

namespace RequestsManagement.Services;

public class RequestStatusService(
    AppDbContext db,
    ICurrentUser currentUser,
    IRequestSummaryCache summaryCache,
    ILogger<RequestStatusService> logger) : IRequestStatusService
{
    public async Task<RequestDto> UpdateStatusAsync(int id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default)
    {
        var changedBy = currentUser.UserName;
        var status = StatusUpdateValidation.TargetStatus(dto.Status);
        var rowVersion = StatusUpdateValidation.RowVersion(dto.RowVersion);

        var entity = await db.Requests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException(RequestMessages.NotFound(id));

        // Before the transition check: stale data means "reload" (409), not a 400 about a status the client never saw.
        if (IsStale(entity, rowVersion))
        {
            throw new ConcurrencyConflictException(RequestMessages.Conflict(id));
        }

        StatusTransitionPolicy.EnsureAllowed(entity.Status, status);

        if (!await TrySaveStatusChangeAsync(entity, rowVersion, status, changedBy, cancellationToken))
        {
            throw new ConcurrencyConflictException(RequestMessages.Conflict(id));
        }

        return entity.ToDto();
    }

    public async Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(BulkUpdateStatusDto dto, CancellationToken cancellationToken = default)
    {
        var changedBy = currentUser.UserName;
        var status = StatusUpdateValidation.TargetStatus(dto.Status);
        var rowVersions = StatusUpdateValidation.BulkItems(dto.Items);

        var ids = rowVersions.Keys.ToList();
        var entities = await db.Requests
            .Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        // Partial success: one transaction per request, so one conflict cannot roll back the others.
        var results = new List<BulkUpdateStatusItemResultDto>(dto.Items.Count);
        foreach (var item in dto.Items)
        {
            results.Add(await UpdateBulkItemAsync(item.Id, entities.GetValueOrDefault(item.Id), rowVersions[item.Id], status, changedBy, cancellationToken));
        }

        var updated = results.Count(r => r.Outcome == BulkUpdateOutcome.Updated);
        logger.LogInformation("Bulk status update to {Status}: {Updated} of {Requested} updated, {Failed} failed",
            status, updated, results.Count, results.Count - updated);

        return new BulkUpdateStatusResultDto
        {
            Requested = results.Count,
            Updated = updated,
            Failed = results.Count - updated,
            Results = results
        };
    }

    private async Task<BulkUpdateStatusItemResultDto> UpdateBulkItemAsync(
        int id, RequestEntity? entity, byte[] rowVersion, RequestStatus status, string changedBy, CancellationToken cancellationToken)
    {
        if (entity is null)
        {
            return Failed(id, BulkUpdateOutcome.NotFound, RequestMessages.NotFound(id));
        }

        if (IsStale(entity, rowVersion))
        {
            return Failed(id, BulkUpdateOutcome.Conflict, RequestMessages.Conflict(id));
        }

        if (StatusTransitionPolicy.Validate(entity.Status, status) is { } transitionError)
        {
            return Failed(id, BulkUpdateOutcome.InvalidTransition, transitionError);
        }

        if (!await TrySaveStatusChangeAsync(entity, rowVersion, status, changedBy, cancellationToken))
        {
            return Failed(id, BulkUpdateOutcome.Conflict, RequestMessages.Conflict(id));
        }

        return new BulkUpdateStatusItemResultDto { Id = id, Outcome = BulkUpdateOutcome.Updated, Request = entity.ToDto() };

        static BulkUpdateStatusItemResultDto Failed(int id, BulkUpdateOutcome outcome, string error) =>
            new() { Id = id, Outcome = outcome, Error = error };
    }

    // The status change and its audit row are saved by one SaveChanges (one transaction), guarded by
    // the client's RowVersion: if the UPDATE matches no row, the audit INSERT is rolled back with it.
    private async Task<bool> TrySaveStatusChangeAsync(
        RequestEntity entity, byte[] rowVersion, RequestStatus status, string changedBy, CancellationToken cancellationToken)
    {
        db.Entry(entity).Property(r => r.RowVersion).OriginalValue = rowVersion;

        var now = DateTime.UtcNow;
        var previousStatus = entity.Status;

        var history = new RequestStatusHistoryEntity
        {
            RequestId = entity.Id,
            PreviousStatus = previousStatus,
            NewStatus = status,
            ChangedAt = now,
            ChangedBy = changedBy
        };
        db.RequestStatusHistory.Add(history);

        entity.Status = status;
        entity.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Status update of request {RequestId} lost a concurrent write race (detected by the database)", entity.Id);
            // Detached so the next bulk item's SaveChanges does not retry this rolled-back change.
            db.Entry(history).State = EntityState.Detached;
            db.Entry(entity).State = EntityState.Detached;
            return false;
        }

        // After the commit: invalidating earlier would let a concurrent reader cache the old numbers.
        summaryCache.Invalidate();

        // ChangedBy is not logged: personal data, already stored in the audit table.
        logger.LogInformation("Request {RequestId} status changed from {PreviousStatus} to {NewStatus}",
            entity.Id, previousStatus, status);
        return true;
    }

    private bool IsStale(RequestEntity entity, byte[] rowVersion)
    {
        if (entity.RowVersion.AsSpan().SequenceEqual(rowVersion))
        {
            return false;
        }

        logger.LogWarning("Status update of request {RequestId} rejected: client RowVersion is stale", entity.Id);
        return true;
    }
}
