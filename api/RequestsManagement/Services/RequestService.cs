using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;
using RequestsManagement.Exceptions;
using RequestsManagement.Mapping;
using RequestsManagement.Models;
using RequestsManagement.Policies;

namespace RequestsManagement.Services;

public class RequestService(AppDbContext db, ICurrentUser currentUser) : IRequestService
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

    public async Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
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

        if (!Enum.IsDefined(dto.Status))
        {
            throw new ValidationException($"'{dto.Status}' is not a valid request status.");
        }

        byte[] rowVersion;
        try
        {
            rowVersion = Convert.FromBase64String(dto.RowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException("RowVersion must be a valid base64-encoded value.");
        }

        var entity = await db.Requests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException($"Request with id {id} was not found.");
        }

        // Early stale check: if the client's version is outdated, its view of the current status is
        // too, so report 409 (reload) rather than a misleading 400 for a transition it never saw.
        // Not a substitute for the database check below, which still catches concurrent writers.
        if (!entity.RowVersion.AsSpan().SequenceEqual(rowVersion))
        {
            throw new ConcurrencyConflictException(
                $"Request {id} was modified by another user since it was loaded. Reload and try again.");
        }

        StatusTransitionPolicy.EnsureAllowed(entity.Status, dto.Status);

        db.Entry(entity).Property(r => r.RowVersion).OriginalValue = rowVersion;

        var now = DateTime.UtcNow;

        // Audit row is saved by the same SaveChanges (one transaction) as the status change:
        // if the RowVersion-guarded UPDATE fails, the INSERT is rolled back with it, so there is
        // never history without a change or a change without history.
        db.RequestStatusHistory.Add(new RequestStatusHistoryEntity
        {
            RequestId = entity.Id,
            PreviousStatus = entity.Status,
            NewStatus = dto.Status,
            ChangedAt = now,
            ChangedBy = changedBy
        });

        entity.Status = dto.Status;
        entity.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                $"Request {id} was modified by another user since it was loaded. Reload and try again.");
        }

        return entity.ToDto();
    }

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
