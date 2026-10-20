using Microsoft.Extensions.Caching.Memory;
using RequestsManagement.DTOs;

namespace RequestsManagement.Services;

/// <summary>
/// In-process cache for the summary. Why the summary: it aggregates the whole table on every page
/// load, its result is identical for all users, and it is tiny (7 numbers). Filtered listings are
/// not cached: their key space is effectively unbounded and their invalidation would be complex.
/// </summary>
/// <remarks>
/// The key carries a generation number that <see cref="Invalidate"/> bumps. A reader captures the
/// generation before computing and stores the result under that generation's key, so a value
/// computed from data read *before* a concurrent change commits can only land under a key nobody
/// reads anymore, instead of overwriting the invalidation with stale numbers. Lock-free, and the
/// same pattern works for a distributed cache (generation kept in Redis via INCR).
/// </remarks>
public sealed class MemoryRequestSummaryCache(IMemoryCache cache, ILogger<MemoryRequestSummaryCache> logger) : IRequestSummaryCache
{
    // Safety net only: correctness after a status change comes from explicit invalidation.
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    private long _generation;

    public async Task<RequestSummaryDto> GetOrCreateAsync(Func<CancellationToken, Task<RequestSummaryDto>> factory, CancellationToken cancellationToken)
    {
        var key = CacheKey(Interlocked.Read(ref _generation));

        if (cache.TryGetValue(key, out RequestSummaryDto? cached) && cached is not null)
        {
            logger.LogDebug("Summary cache hit");
            return cached;
        }

        logger.LogDebug("Summary cache miss; computing aggregations");
        var summary = await factory(cancellationToken);
        cache.Set(key, summary, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Expiration });
        return summary;
    }

    public void Invalidate()
    {
        var previous = Interlocked.Increment(ref _generation) - 1;
        cache.Remove(CacheKey(previous));
        logger.LogDebug("Summary cache invalidated");
    }

    private static string CacheKey(long generation) => $"requests:summary:{generation}";
}
