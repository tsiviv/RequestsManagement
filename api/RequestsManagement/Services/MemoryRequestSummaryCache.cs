using Microsoft.Extensions.Caching.Memory;
using RequestsManagement.DTOs;

namespace RequestsManagement.Services;

/// <summary>
/// In-process cache for the summary. Why the summary: it aggregates the whole table on every page
/// load, its result is identical for all users, and it is tiny (7 numbers). Filtered listings are
/// not cached: their key space is effectively unbounded and their invalidation would be complex.
/// </summary>
public sealed class MemoryRequestSummaryCache(IMemoryCache cache, ILogger<MemoryRequestSummaryCache> logger) : IRequestSummaryCache
{
    // Safety net only: correctness after a status change comes from explicit invalidation.
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    private const string CacheKey = "requests:summary";

    public async Task<RequestSummaryDto> GetOrCreateAsync(Func<CancellationToken, Task<RequestSummaryDto>> factory, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out RequestSummaryDto? cached) && cached is not null)
        {
            logger.LogDebug("Summary cache hit");
            return cached;
        }

        logger.LogDebug("Summary cache miss; computing aggregations");
        var summary = await factory(cancellationToken);
        cache.Set(CacheKey, summary, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Expiration });
        return summary;
    }
}
