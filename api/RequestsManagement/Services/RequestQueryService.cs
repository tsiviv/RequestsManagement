using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;
using RequestsManagement.Exceptions;
using RequestsManagement.Mapping;

namespace RequestsManagement.Services;

public class RequestQueryService(AppDbContext db, IRequestSummaryCache summaryCache) : IRequestQueryService
{
    public async Task<PagedResultDto<RequestDto>> GetRequestsAsync(RequestQueryDto query, CancellationToken cancellationToken = default)
    {
        var (sortBy, descending) = RequestSorting.Resolve(query);
        RequestFilters.Validate(query);

        var requests = db.Requests.AsNoTracking().ApplyFilters(query);

        // Unfiltered, the total is the cached summary's total, saving a COUNT over the whole table.
        var totalCount = RequestFilters.HasAny(query)
            ? await requests.CountAsync(cancellationToken)
            : (await GetSummaryAsync(cancellationToken)).Total;

        // long: an extreme Page (e.g. int.MaxValue) must not overflow the Int32 Skip.
        var skip = (long)(query.Page - 1) * query.PageSize;

        List<RequestDto> items = [];
        if (skip < totalCount)
        {
            // Deferred join: select the page's Ids from the index first, then load only those rows,
            // instead of a key lookup for every skipped row (see docs/PERFORMANCE.md).
            var pageIds = requests.ApplySort(sortBy, descending)
                .Select(r => r.Id)
                .Skip((int)skip)
                .Take(query.PageSize);

            items = await db.Requests.AsNoTracking()
                .Where(r => pageIds.Contains(r.Id))
                .ApplySort(sortBy, descending)
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

    public async Task<RequestFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await db.Requests.AsNoTracking()
            .Select(r => r.OrganizationName).Distinct().OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var assignees = await db.Requests.AsNoTracking()
            .Select(r => r.AssignedTo).Distinct().OrderBy(n => n)
            .ToListAsync(cancellationToken);

        return new RequestFilterOptionsDto { Organizations = organizations, Assignees = assignees };
    }

    public async Task<IReadOnlyList<RequestStatusHistoryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var history = await db.RequestStatusHistory.AsNoTracking()
            .Where(h => h.RequestId == id)
            .OrderByDescending(h => h.ChangedAt)
            .ThenByDescending(h => h.Id)
            .Select(RequestMappings.ToHistoryDtoProjection)
            .ToListAsync(cancellationToken);

        // Only an empty history needs to tell "no changes yet" (200 []) from "no such request" (404).
        if (history.Count == 0 && !await db.Requests.AnyAsync(r => r.Id == id, cancellationToken))
        {
            throw new NotFoundException(RequestMessages.NotFound(id));
        }

        return history;
    }

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

    // Values with no requests appear as 0 (GROUP BY omits them); the UI expects every key.
    private static Dictionary<string, int> WithAllEnumKeys<TEnum>(Dictionary<TEnum, int> counts) where TEnum : struct, Enum
    {
        var result = new Dictionary<string, int>();
        foreach (var value in Enum.GetValues<TEnum>())
        {
            result[value.ToString()] = counts.GetValueOrDefault(value);
        }
        return result;
    }
}
