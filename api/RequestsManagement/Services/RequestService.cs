using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Mapping;
using RequestsManagement.Models;
using RequestsManagement.Policies;

namespace RequestsManagement.Services;

public class RequestService(
    AppDbContext db,
    ICurrentUser currentUser,
    IRequestSummaryCache summaryCache,
    ILogger<RequestService> logger) : IRequestService
{
    private static readonly string[] SortableFields =
        ["status", "priority", "createdat", "updatedat"];

    public async Task<PagedResultDto<RequestDto>> GetRequestsAsync(RequestQueryDto query, CancellationToken cancellationToken = default)
    {
        ValidateSort(query.SortBy, query.SortDirection);
        ValidateDateRange(query.CreatedFrom, query.CreatedTo);

        IQueryable<RequestEntity> requests = db.Requests.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            requests = requests.Where(r => r.Title.Contains(search) || r.OrganizationName.Contains(search));
        }

        if (query.Status.HasValue)
        {
            requests = requests.Where(r => r.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            requests = requests.Where(r => r.Priority == query.Priority.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.OrganizationName))
        {
            var organizationName = query.OrganizationName.Trim();
            requests = requests.Where(r => r.OrganizationName == organizationName);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedTo))
        {
            var assignedTo = query.AssignedTo.Trim();
            requests = requests.Where(r => r.AssignedTo == assignedTo);
        }

        if (query.CreatedFrom.HasValue)
        {
            requests = requests.Where(r => r.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            requests = requests.Where(r => r.CreatedAt < query.CreatedTo.Value);
        }

        var totalCount = await requests.CountAsync(cancellationToken);

        // Computed as a long so an extreme but otherwise valid Int32 page (e.g. int.MaxValue)
        // can never overflow the Int32 Skip/Take EF Core requires. When the offset is at or
        // past totalCount there is nothing to fetch, so skip the query entirely.
        var skip = (long)(query.Page - 1) * query.PageSize;

        List<RequestDto> items;
        if (skip >= totalCount)
        {
            items = [];
        }
        else
        {
            requests = ApplySort(requests, query.SortBy, query.SortDirection);

            items = await requests
                .Skip((int)skip)
                .Take(query.PageSize)
                .Select(RequestMappings.ToDtoProjection)
                .ToListAsync(cancellationToken);
        }

        return new PagedResultDto<RequestDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default) =>
        summaryCache.GetOrCreateAsync(ComputeSummaryAsync, cancellationToken);

    private async Task<RequestSummaryDto> ComputeSummaryAsync(CancellationToken cancellationToken)
    {
        var total = await db.Requests.AsNoTracking().CountAsync(cancellationToken);

        var byStatus = await db.Requests.AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var byPriority = await db.Requests.AsNoTracking()
            .GroupBy(r => r.Priority)
            .Select(g => new { Priority = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new RequestSummaryDto
        {
            Total = total,
            ByStatus = WithAllEnumKeys(byStatus.ToDictionary(x => x.Status, x => x.Count)),
            ByPriority = WithAllEnumKeys(byPriority.ToDictionary(x => x.Priority, x => x.Count))
        };
    }

    // The frontend's summary type is a complete map over every enum value, so a
    // status/priority with zero matching requests must still appear (as 0) rather
    // than being omitted, which is what a bare GroupBy would otherwise produce.
    private static Dictionary<string, int> WithAllEnumKeys<TEnum>(Dictionary<TEnum, int> counts) where TEnum : struct, Enum
    {
        var result = new Dictionary<string, int>();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            result[value.ToString()] = counts.GetValueOrDefault(value);
        }
        return result;
    }

    public async Task<IReadOnlyList<RequestStatusHistoryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken = default)
    {
        // Seeks IX_RequestStatusHistory_RequestId_ChangedAt, read backward for newest first
        // (Id breaks ties between changes with the same timestamp).
        var history = await db.RequestStatusHistory.AsNoTracking()
            .Where(h => h.RequestId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.Id)
            .Select(RequestMappings.ToHistoryDtoProjection)
            .ToListAsync(cancellationToken);

        // Only an empty result needs the existence check that separates "no changes yet" (200 [])
        // from "no such request" (404), so the common case costs a single query.
        if (history.Count == 0 && !await db.Requests.AnyAsync(r => r.Id == id, cancellationToken))
        {
            throw new NotFoundException($"Request with id {id} was not found.");
        }

        return history;
    }

    public async Task<RequestDto> UpdateStatusAsync(int id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default)
    {
        // Resolved first: like the checks below, a missing/invalid acting user is bad input (400)
        // and must be rejected before touching the database.
        var changedBy = currentUser.UserName;
        var status = ValidateTargetStatus(dto.Status);
        var rowVersion = ParseRowVersion(dto.RowVersion);

        var entity = await db.Requests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException($"Request with id {id} was not found.");
        }

        // Early stale check: if the client's version is outdated, its view of the current status is
        // too, so report 409 (reload) rather than a misleading 400 for a transition it never saw.
        // Not a substitute for the database check in TrySaveStatusChangeAsync, which still catches
        // concurrent writers.
        if (IsStale(entity, rowVersion))
        {
            throw new ConcurrencyConflictException(ConflictMessage(id));
        }

        StatusTransitionPolicy.EnsureAllowed(entity.Status, status);

        if (!await TrySaveStatusChangeAsync(entity, rowVersion, status, changedBy, cancellationToken))
        {
            throw new ConcurrencyConflictException(ConflictMessage(id));
        }

        return entity.ToDto();
    }

    public async Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(BulkUpdateStatusDto dto, CancellationToken cancellationToken = default)
    {
        // Request-level validation (400, nothing updated): problems with the request as a whole are
        // client bugs, not the state of a particular request.
        var changedBy = currentUser.UserName;
        var status = ValidateTargetStatus(dto.Status);

        if (dto.Items is not { Count: > 0 and <= BulkUpdateStatusDto.MaxItems })
        {
            throw new ValidationException($"Items must contain between 1 and {BulkUpdateStatusDto.MaxItems} requests.");
        }

        var duplicateIds = dto.Items.GroupBy(i => i.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
        {
            throw new ValidationException($"Each request may appear only once. Duplicate ids: {string.Join(", ", duplicateIds)}.");
        }

        var rowVersions = dto.Items.ToDictionary(i => i.Id, i => ParseRowVersion(i.RowVersion, i.Id));

        // One query for all requests instead of one per item.
        var ids = rowVersions.Keys.ToList();
        var entities = await db.Requests
            .Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        // Partial success: each request is saved by its own SaveChanges (its own transaction, status
        // + audit row together), so a conflict on one request cannot roll back the others.
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
            return Failed(id, BulkUpdateOutcome.NotFound, $"Request with id {id} was not found.");
        }

        // Same order as the single update: a stale client gets "reload", not a transition error.
        if (IsStale(entity, rowVersion))
        {
            return Failed(id, BulkUpdateOutcome.Conflict, ConflictMessage(id));
        }

        if (StatusTransitionPolicy.Validate(entity.Status, status) is { } transitionError)
        {
            return Failed(id, BulkUpdateOutcome.InvalidTransition, transitionError);
        }

        if (!await TrySaveStatusChangeAsync(entity, rowVersion, status, changedBy, cancellationToken))
        {
            return Failed(id, BulkUpdateOutcome.Conflict, ConflictMessage(id));
        }

        return new BulkUpdateStatusItemResultDto { Id = id, Outcome = BulkUpdateOutcome.Updated, Request = entity.ToDto() };

        static BulkUpdateStatusItemResultDto Failed(int id, BulkUpdateOutcome outcome, string error) =>
            new() { Id = id, Outcome = outcome, Error = error };
    }

    /// <summary>
    /// Applies the status change and its audit row, and saves both in one SaveChanges (one
    /// transaction) guarded by the client's RowVersion. Returns false if the database reports a
    /// concurrent write; the rolled-back changes are then detached so a later SaveChanges on the
    /// same context (the next bulk item) does not retry them.
    /// </summary>
    private async Task<bool> TrySaveStatusChangeAsync(
        RequestEntity entity, byte[] rowVersion, RequestStatus status, string changedBy, CancellationToken cancellationToken)
    {
        db.Entry(entity).Property(r => r.RowVersion).OriginalValue = rowVersion;

        var now = DateTime.UtcNow;
        var previousStatus = entity.Status;

        // Saved by the same SaveChanges as the status change: if the RowVersion-guarded UPDATE
        // fails, the INSERT is rolled back with it, so there is never history without a change
        // or a change without history.
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
            // A writer committed between our load and our save; the RowVersion-guarded UPDATE
            // matched no row and the transaction (including the audit row) was rolled back.
            logger.LogWarning("Status update of request {RequestId} lost a concurrent write race (detected by the database)", entity.Id);
            db.Entry(history).State = EntityState.Detached;
            db.Entry(entity).State = EntityState.Detached;
            return false;
        }

        // ChangedBy is deliberately not logged: it is personal data and already lives in the audit table.
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

    private static RequestStatus ValidateTargetStatus(RequestStatus? status)
    {
        if (status is null)
        {
            throw new ValidationException("Status is required.");
        }

        if (!Enum.IsDefined(status.Value))
        {
            throw new ValidationException($"'{status}' is not a valid request status.");
        }

        return status.Value;
    }

    private static byte[] ParseRowVersion(string rowVersion, int? requestId = null)
    {
        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException(requestId is null
                ? "RowVersion must be a valid base64-encoded value."
                : $"RowVersion of request {requestId} must be a valid base64-encoded value.");
        }
    }

    private static string ConflictMessage(int id) =>
        $"Request {id} was modified by another user since it was loaded. Reload and try again.";

    private static void ValidateSort(string sortBy, string sortDirection)
    {
        if (!SortableFields.Contains(sortBy.Trim().ToLowerInvariant()))
        {
            throw new ValidationException(
                $"Invalid sortBy '{sortBy}'. Allowed values: {string.Join(", ", SortableFields)}.");
        }

        if (!string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"Invalid sortDirection '{sortDirection}'. Allowed values: asc, desc.");
        }
    }

    private static void ValidateDateRange(DateTime? createdFrom, DateTime? createdTo)
    {
        if (createdFrom.HasValue && createdTo.HasValue && createdFrom.Value >= createdTo.Value)
        {
            throw new ValidationException("CreatedFrom must be earlier than CreatedTo.");
        }
    }

    private static IQueryable<RequestEntity> ApplySort(IQueryable<RequestEntity> query, string sortBy, string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        var ordered = sortBy.Trim().ToLowerInvariant() switch
        {
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "priority" => descending ? query.OrderByDescending(r => r.Priority) : query.OrderBy(r => r.Priority),
            "updatedat" => descending ? query.OrderByDescending(r => r.UpdatedAt) : query.OrderBy(r => r.UpdatedAt),
            _ => descending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt)
        };

        // Id tiebreaker makes the order total, so OFFSET paging is deterministic: without it, rows
        // sharing a sort value (e.g. ~55K with Status = Completed) may repeat or vanish across pages.
        // Free in SQL Server, since every non-clustered index key already ends with the clustered Id.
        return descending ? ordered.ThenByDescending(r => r.Id) : ordered.ThenBy(r => r.Id);
    }
}
