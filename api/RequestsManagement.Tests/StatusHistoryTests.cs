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
            await CreateService(context, new StubCurrentUser("דנה כהן")).UpdateStatusAsync(id,
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
            var updated = await CreateService(context).UpdateStatusAsync(id,
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
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => CreateService(context).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = RequestStatus.Completed, RowVersion = Convert.ToBase64String(BitConverter.GetBytes(99L)) }));
        }

        await using var verify = CreateContext(dbName);
        Assert.Empty(await verify.RequestStatusHistory.ToListAsync());
    }

    // A conflict detected by the database itself (a writer committing between our load and our
    // SaveChanges) must roll back the history INSERT together with the UPDATE. That relies on the
    // SaveChanges transaction, which the InMemory provider does not have, so it is verified
    // against SQL Server in the integration tests (WORK_PLAN T8.3).

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
