using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;
using RequestsManagement.Exceptions;
using RequestsManagement.Models;
using RequestsManagement.Services.Interfaces;

namespace RequestsManagement.Services;

public class RequestService(AppDbContext db) : IRequestService
{
    private static readonly string[] SortableFields =
        ["status", "priority", "createdat", "updatedat"];

    public async Task<PagedResultDto<RequestDto>> GetRequestsAsync(RequestQueryDto query, CancellationToken cancellationToken = default)
    {
        ValidateSort(query.SortBy, query.SortDirection);

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
                .Select(r => new RequestDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    OrganizationName = r.OrganizationName,
                    Status = r.Status,
                    Priority = r.Priority,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt,
                    RowVersion = Convert.ToBase64String(r.RowVersion)
                })
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

    public async Task<RequestDto> UpdateStatusAsync(int id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default)
    {
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

        db.Entry(entity).Property(r => r.RowVersion).OriginalValue = rowVersion;

        entity.Status = dto.Status;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                $"Request {id} was modified by another user since it was loaded. Reload and try again.");
        }

        return new RequestDto
        {
            Id = entity.Id,
            Title = entity.Title,
            OrganizationName = entity.OrganizationName,
            Status = entity.Status,
            Priority = entity.Priority,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            RowVersion = Convert.ToBase64String(entity.RowVersion)
        };
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

    private static IQueryable<RequestEntity> ApplySort(IQueryable<RequestEntity> query, string sortBy, string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortBy.Trim().ToLowerInvariant() switch
        {
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "priority" => descending ? query.OrderByDescending(r => r.Priority) : query.OrderBy(r => r.Priority),
            "updatedat" => descending ? query.OrderByDescending(r => r.UpdatedAt) : query.OrderBy(r => r.UpdatedAt),
            _ => descending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt)
        };
    }
}
