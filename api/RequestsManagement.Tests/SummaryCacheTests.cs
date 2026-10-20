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
            Assert.Equal(2, (await CreateService(first, summaryCache: cache).GetSummaryAsync()).Total);
        }

        // A row added behind the service's back (no invalidation): a cached answer still says 2.
        await AddRequestAsync(dbName);

        await using var second = CreateContext(dbName);
        Assert.Equal(2, (await CreateService(second, summaryCache: cache).GetSummaryAsync()).Total);
    }

    [Fact]
    public async Task AfterExpiration_SummaryIsRecomputed()
    {
        var dbName = await SeedAsync(2);
        var clock = new FakeClock();
        var cache = CreateSummaryCache(new MemoryCache(new MemoryCacheOptions { Clock = clock }));

        await using (var context = CreateContext(dbName))
        {
            await CreateService(context, summaryCache: cache).GetSummaryAsync();
        }
        await AddRequestAsync(dbName);

        clock.Advance(MemoryRequestSummaryCache.Expiration - TimeSpan.FromSeconds(1));
        await using (var context = CreateContext(dbName))
        {
            Assert.Equal(2, (await CreateService(context, summaryCache: cache).GetSummaryAsync()).Total);
        }

        clock.Advance(TimeSpan.FromSeconds(2));
        await using (var context = CreateContext(dbName))
        {
            Assert.Equal(3, (await CreateService(context, summaryCache: cache).GetSummaryAsync()).Total);
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
