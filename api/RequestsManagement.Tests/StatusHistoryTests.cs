using Microsoft.EntityFrameworkCore;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using Xunit;
using static RequestsManagement.Tests.TestData;

namespace RequestsManagement.Tests;

public class StatusHistoryTests
{
    [Fact]
    public async Task UpdateStatusAsync_WritesHistoryRow_WithPreviousNewUserAndSameTimestamp()
    {
        var (dbName, id, rowVersion) = await SeedRequestAsync(RequestStatus.New);

        await using (var context = CreateContext(dbName))
        {
            await CreateStatusService(context, new StubCurrentUser("דנה כהן")).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion });
        }

        await using var verify = CreateContext(dbName);
        var request = await verify.Requests.SingleAsync(r => r.Id == id);
        var history = Assert.Single(await verify.RequestStatusHistory.ToListAsync());
        Assert.Equal(id, history.RequestId);
        Assert.Equal(RequestStatus.New, history.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, history.NewStatus);
        Assert.Equal("דנה כהן", history.ChangedBy);
        Assert.Equal(request.UpdatedAt, history.ChangedAt);
    }

    [Fact]
    public async Task SuccessiveUpdates_EachAppendTheirOwnRow_ChainingStatuses()
    {
        var (dbName, id, rowVersion) = await SeedRequestAsync(RequestStatus.New);

        foreach (var next in new[] { RequestStatus.InProgress, RequestStatus.Waiting, RequestStatus.Completed })
        {
            await using var context = CreateContext(dbName);
            var updated = await CreateStatusService(context).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = next, RowVersion = rowVersion });
            rowVersion = updated.RowVersion;
        }

        await using var verify = CreateContext(dbName);
        var history = await verify.RequestStatusHistory.OrderBy(h => h.Id).ToListAsync();
        Assert.Equal(
            [
                (RequestStatus.New, RequestStatus.InProgress),
                (RequestStatus.InProgress, RequestStatus.Waiting),
                (RequestStatus.Waiting, RequestStatus.Completed)
            ],
            history.Select(h => (h.PreviousStatus, h.NewStatus)));
        Assert.All(history, h => Assert.Equal(TestUser, h.ChangedBy));
    }

    [Fact]
    public async Task StaleRowVersion_IsRejectedWithoutWritingHistory()
    {
        var (dbName, id, _) = await SeedRequestAsync(RequestStatus.InProgress);

        await using (var context = CreateContext(dbName))
        {
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => CreateStatusService(context).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = RequestStatus.Completed, RowVersion = Convert.ToBase64String(BitConverter.GetBytes(99L)) }));
        }

        await using var verify = CreateContext(dbName);
        Assert.Empty(await verify.RequestStatusHistory.ToListAsync());
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsOnlyThisRequestsChanges_NewestFirst()
    {
        var (dbName, id, rowVersion) = await SeedRequestAsync(RequestStatus.New);
        int otherId;
        string otherRowVersion;
        await using (var seed = CreateContext(dbName))
        {
            var other = NewRequest("B", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow.AddDays(-1));
            seed.Requests.Add(other);
            await seed.SaveChangesAsync();
            (otherId, otherRowVersion) = (other.Id, Convert.ToBase64String(other.RowVersion));
        }

        foreach (var next in new[] { RequestStatus.InProgress, RequestStatus.Waiting })
        {
            await using var context = CreateContext(dbName);
            rowVersion = (await CreateStatusService(context).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = next, RowVersion = rowVersion })).RowVersion;
        }
        await using (var context = CreateContext(dbName))
        {
            await CreateStatusService(context).UpdateStatusAsync(otherId,
                new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = otherRowVersion });
        }

        await using var read = CreateContext(dbName);
        var history = await CreateQueryService(read).GetHistoryAsync(id);

        Assert.Equal(2, history.Count);
        Assert.Equal((RequestStatus.InProgress, RequestStatus.Waiting), (history[0].PreviousStatus, history[0].NewStatus));
        Assert.Equal((RequestStatus.New, RequestStatus.InProgress), (history[1].PreviousStatus, history[1].NewStatus));
        Assert.True(history[0].ChangedAt >= history[1].ChangedAt);
        Assert.All(history, h => Assert.Equal(TestUser, h.ChangedBy));
    }

    [Fact]
    public async Task GetHistoryAsync_SameTimestamp_BreaksTieByNewestId()
    {
        var (dbName, id, _) = await SeedRequestAsync(RequestStatus.Waiting);
        var sameMoment = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        await using (var seed = CreateContext(dbName))
        {
            seed.RequestStatusHistory.AddRange(
                new() { RequestId = id, PreviousStatus = RequestStatus.New, NewStatus = RequestStatus.InProgress, ChangedAt = sameMoment, ChangedBy = "first" },
                new() { RequestId = id, PreviousStatus = RequestStatus.InProgress, NewStatus = RequestStatus.Waiting, ChangedAt = sameMoment, ChangedBy = "second" });
            await seed.SaveChangesAsync();
        }

        await using var read = CreateContext(dbName);
        var history = await CreateQueryService(read).GetHistoryAsync(id);

        Assert.Equal(["second", "first"], history.Select(h => h.ChangedBy));
    }

    [Fact]
    public async Task GetHistoryAsync_ExistingRequestWithoutChanges_ReturnsEmpty()
    {
        var (dbName, id, _) = await SeedRequestAsync(RequestStatus.New);

        await using var read = CreateContext(dbName);
        Assert.Empty(await CreateQueryService(read).GetHistoryAsync(id));
    }

    [Fact]
    public async Task GetHistoryAsync_MissingRequest_ThrowsNotFound()
    {
        await using var read = CreateContext(Guid.NewGuid().ToString());
        await Assert.ThrowsAsync<NotFoundException>(() => CreateQueryService(read).GetHistoryAsync(999));
    }

    // Rollback of the history row on a database-detected conflict needs a real transaction, so it is
    // tested against SQL Server (ConcurrencyApiTests).

    private static async Task<(string DbName, int Id, string RowVersion)> SeedRequestAsync(RequestStatus status)
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var entity = NewRequest("A", "Org", status, RequestPriority.Low, DateTime.UtcNow.AddDays(-1));
        context.Requests.Add(entity);
        await context.SaveChangesAsync();
        return (dbName, entity.Id, Convert.ToBase64String(entity.RowVersion));
    }
}
