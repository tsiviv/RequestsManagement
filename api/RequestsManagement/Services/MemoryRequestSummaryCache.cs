using Microsoft.Extensions.Caching.Memory;
using RequestsManagement.DTOs;

namespace RequestsManagement.Services;

// The key carries a generation that Invalidate() bumps: a value computed before a concurrent
// change commits is stored under a key nobody reads anymore, never served as stale.
public sealed class MemoryRequestSummaryCache(IMemoryCache cache, ILogger<MemoryRequestSummaryCache> logger) : IRequestSummaryCache
{
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
