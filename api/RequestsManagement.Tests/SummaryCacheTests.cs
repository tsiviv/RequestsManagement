using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Services;
using Xunit;
using static RequestsManagement.Tests.TestData;

namespace RequestsManagement.Tests;

public class SummaryCacheTests
{
    [Fact]
    public async Task SecondRequest_IsServedFromCache_WithoutRecomputing()
    {
        var dbName = await SeedAsync(2);
        var cache = CreateSummaryCache();

        await using (var first = CreateContext(dbName))
        {
            Assert.Equal(2, (await CreateQueryService(first, summaryCache: cache).GetSummaryAsync()).Total);
        }

        // A row added behind the service's back (no invalidation): a cached answer still says 2.
        await AddRequestAsync(dbName);

        await using var second = CreateContext(dbName);
        Assert.Equal(2, (await CreateQueryService(second, summaryCache: cache).GetSummaryAsync()).Total);
    }

    [Fact]
    public async Task AfterExpiration_SummaryIsRecomputed()
    {
        var dbName = await SeedAsync(2);
        var clock = new FakeClock();
        var cache = CreateSummaryCache(new MemoryCache(new MemoryCacheOptions { Clock = clock }));

        await using (var context = CreateContext(dbName))
        {
            await CreateQueryService(context, summaryCache: cache).GetSummaryAsync();
        }
        await AddRequestAsync(dbName);

        clock.Advance(MemoryRequestSummaryCache.Expiration - TimeSpan.FromSeconds(1));
        await using (var context = CreateContext(dbName))
        {
            Assert.Equal(2, (await CreateQueryService(context, summaryCache: cache).GetSummaryAsync()).Total);
        }

        clock.Advance(TimeSpan.FromSeconds(2));
        await using (var context = CreateContext(dbName))
        {
            Assert.Equal(3, (await CreateQueryService(context, summaryCache: cache).GetSummaryAsync()).Total);
        }
    }

    [Fact]
    public async Task FailedComputation_IsNotCached()
    {
        var cache = CreateSummaryCache();
        var calls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetOrCreateAsync(_ =>
        {
            calls++;
            throw new InvalidOperationException("database unavailable");
        }, CancellationToken.None));

        var summary = await cache.GetOrCreateAsync(_ =>
        {
            calls++;
            return Task.FromResult(new RequestSummaryDto { Total = 7 });
        }, CancellationToken.None);

        Assert.Equal(7, summary.Total);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task UnfilteredList_TakesItsTotalFromTheCachedSummary_FilteredListCountsExactly()
    {
        var dbName = await SeedAsync(3);
        var cache = CreateSummaryCache();
        await using (var context = CreateContext(dbName))
        {
            await CreateQueryService(context, summaryCache: cache).GetSummaryAsync();
        }

        // A row added behind the cache's back: only a real COUNT would see it.
        await AddRequestAsync(dbName);

        await using var read = CreateContext(dbName);
        var service = CreateQueryService(read, summaryCache: cache);
        Assert.Equal(3, (await service.GetRequestsAsync(new RequestQueryDto())).TotalCount);
        Assert.Equal(4, (await service.GetRequestsAsync(new RequestQueryDto { Status = RequestStatus.New })).TotalCount);
    }

    [Fact]
    public async Task SingleUpdate_InvalidatesSummary_SoTheNextReadIsFresh()
    {
        var (dbName, id, rowVersion) = await SeedOneAsync(RequestStatus.New);
        var cache = CreateSummaryCache();
        Assert.Equal(1, (await GetSummaryAsync(dbName, cache)).ByStatus["New"]);

        await using (var context = CreateContext(dbName))
        {
            await CreateStatusService(context, summaryCache: cache).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion });
        }

        var summary = await GetSummaryAsync(dbName, cache);
        Assert.Equal(0, summary.ByStatus["New"]);
        Assert.Equal(1, summary.ByStatus["InProgress"]);
    }

    [Fact]
    public async Task BulkUpdate_WithAnUpdatedItem_InvalidatesSummary()
    {
        var (dbName, id, rowVersion) = await SeedOneAsync(RequestStatus.New);
        var cache = CreateSummaryCache();
        await GetSummaryAsync(dbName, cache);

        await using (var context = CreateContext(dbName))
        {
            await CreateStatusService(context, summaryCache: cache).BulkUpdateStatusAsync(new BulkUpdateStatusDto
            {
                Status = RequestStatus.InProgress,
                Items = [new() { Id = id, RowVersion = rowVersion }, new() { Id = 999, RowVersion = rowVersion }]
            });
        }

        Assert.Equal(1, (await GetSummaryAsync(dbName, cache)).ByStatus["InProgress"]);
    }

    [Fact]
    public async Task FailedUpdates_DoNotInvalidate()
    {
        // Nothing changed, so there is nothing to invalidate; proven by a row added behind the
        // cache's back staying invisible after the failed single and bulk updates.
        var (dbName, id, _) = await SeedOneAsync(RequestStatus.New);
        var cache = CreateSummaryCache();
        Assert.Equal(1, (await GetSummaryAsync(dbName, cache)).Total);
        await AddRequestAsync(dbName);
        var stale = Convert.ToBase64String(BitConverter.GetBytes(99L));

        await using (var context = CreateContext(dbName))
        {
            var service = CreateStatusService(context, summaryCache: cache);
            await Assert.ThrowsAsync<RequestsManagement.Exceptions.ConcurrencyConflictException>(() =>
                service.UpdateStatusAsync(id, new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = stale }));
            var bulk = await service.BulkUpdateStatusAsync(new BulkUpdateStatusDto
            {
                Status = RequestStatus.InProgress,
                Items = [new() { Id = id, RowVersion = stale }]
            });
            Assert.Equal(0, bulk.Updated);
        }

        Assert.Equal(1, (await GetSummaryAsync(dbName, cache)).Total);
    }

    [Fact]
    public async Task ChangeCommittedWhileSummaryIsBeingComputed_StaleResultIsNotServedAfterwards()
    {
        // Reader starts computing from pre-change data; meanwhile a writer commits and invalidates.
        // The reader's stale result must not become the cached value for later readers.
        var cache = CreateSummaryCache();
        var calls = 0;

        var first = await cache.GetOrCreateAsync(_ =>
        {
            calls++;
            cache.Invalidate(); // the concurrent writer's commit + invalidation
            return Task.FromResult(new RequestSummaryDto { Total = 1 });
        }, CancellationToken.None);

        var second = await cache.GetOrCreateAsync(_ =>
        {
            calls++;
            return Task.FromResult(new RequestSummaryDto { Total = 2 });
        }, CancellationToken.None);

        Assert.Equal(1, first.Total);   // the in-flight caller gets what it computed
        Assert.Equal(2, second.Total);  // but later callers recompute instead of reading it
        Assert.Equal(2, calls);
    }

    private static async Task<RequestSummaryDto> GetSummaryAsync(string dbName, IRequestSummaryCache cache)
    {
        await using var context = CreateContext(dbName);
        return await CreateQueryService(context, summaryCache: cache).GetSummaryAsync();
    }

    private static async Task<(string DbName, int Id, string RowVersion)> SeedOneAsync(RequestStatus status)
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var entity = NewRequest("R", "Org", status, RequestPriority.Low, DateTime.UtcNow);
        context.Requests.Add(entity);
        await context.SaveChangesAsync();
        return (dbName, entity.Id, Convert.ToBase64String(entity.RowVersion));
    }

    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => UtcNow += by;
    }

    private static async Task<string> SeedAsync(int count)
    {
        var dbName = Guid.NewGuid().ToString();
        for (var i = 0; i < count; i++)
        {
            await AddRequestAsync(dbName);
        }
        return dbName;
    }

    private static async Task AddRequestAsync(string dbName)
    {
        await using var context = CreateContext(dbName);
        context.Requests.Add(NewRequest("R", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow));
        await context.SaveChangesAsync();
    }
}
