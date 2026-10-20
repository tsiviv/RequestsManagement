using Microsoft.EntityFrameworkCore;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Policies;
using RequestsManagement.Services;
using Xunit;
using static RequestsManagement.Tests.TestData;

namespace RequestsManagement.Tests;

public class StatusTransitionTests
{
    // The agreed transition rules, written out independently of StatusTransitionPolicy so the
    // tests check the policy against the spec rather than against itself.
    private static readonly HashSet<(RequestStatus From, RequestStatus To)> ExpectedAllowed =
    [
        (RequestStatus.New, RequestStatus.InProgress),
        (RequestStatus.InProgress, RequestStatus.Waiting),
        (RequestStatus.InProgress, RequestStatus.Completed),
        (RequestStatus.Waiting, RequestStatus.InProgress),
        (RequestStatus.Waiting, RequestStatus.Completed)
    ];

    private static IEnumerable<(RequestStatus From, RequestStatus To)> AllPairs() =>
        from source in Enum.GetValues<RequestStatus>()
        from target in Enum.GetValues<RequestStatus>()
        select (source, target);

    public static TheoryData<RequestStatus, RequestStatus> AllowedTransitions()
    {
        var data = new TheoryData<RequestStatus, RequestStatus>();
        foreach (var (from, to) in AllPairs().Where(ExpectedAllowed.Contains))
        {
            data.Add(from, to);
        }
        return data;
    }

    // Every other pair, including the four "same status" pairs.
    public static TheoryData<RequestStatus, RequestStatus> ForbiddenTransitions()
    {
        var data = new TheoryData<RequestStatus, RequestStatus>();
        foreach (var (from, to) in AllPairs().Where(p => !ExpectedAllowed.Contains(p)))
        {
            data.Add(from, to);
        }
        return data;
    }

    // --- Policy ----------------------------------------------------------------

    [Fact]
    public void Matrix_CoversEveryPair_FiveAllowedElevenForbidden()
    {
        Assert.Equal(5, AllowedTransitions().Count);
        Assert.Equal(11, ForbiddenTransitions().Count);
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Policy_AllowsTransition(RequestStatus from, RequestStatus to)
    {
        Assert.True(StatusTransitionPolicy.IsAllowed(from, to));
        StatusTransitionPolicy.EnsureAllowed(from, to); // does not throw
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void Policy_RejectsTransition(RequestStatus from, RequestStatus to)
    {
        Assert.False(StatusTransitionPolicy.IsAllowed(from, to));
        Assert.Throws<ValidationException>(() => StatusTransitionPolicy.EnsureAllowed(from, to));
    }

    [Fact]
    public void Policy_SameStatus_ExplainsRequestIsAlreadyInThatStatus()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            StatusTransitionPolicy.EnsureAllowed(RequestStatus.Waiting, RequestStatus.Waiting));
        Assert.Contains("כבר בסטטוס 'ממתין'", ex.Message);
    }

    [Fact]
    public void Policy_FromCompleted_ExplainsFinalStatus()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            StatusTransitionPolicy.EnsureAllowed(RequestStatus.Completed, RequestStatus.InProgress));
        Assert.Contains("סטטוס סופי", ex.Message);
    }

    // --- Service: the API path uses the same policy -------------------------

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public async Task UpdateStatusAsync_AllowedTransition_Succeeds(RequestStatus from, RequestStatus to)
    {
        var (dbName, id, rowVersion) = await SeedRequestAsync(from);

        await using var context = CreateContext(dbName);
        var result = await CreateService(context).UpdateStatusAsync(id,
            new UpdateRequestStatusDto { Status = to, RowVersion = rowVersion });

        Assert.Equal(to, result.Status);
        await using var verify = CreateContext(dbName);
        Assert.Equal(to, (await verify.Requests.SingleAsync(r => r.Id == id)).Status);
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public async Task UpdateStatusAsync_ForbiddenTransition_ThrowsValidation_AndChangesNothing(RequestStatus from, RequestStatus to)
    {
        var (dbName, id, rowVersion) = await SeedRequestAsync(from);
        DateTime updatedAtBefore;
        await using (var before = CreateContext(dbName))
        {
            updatedAtBefore = (await before.Requests.SingleAsync(r => r.Id == id)).UpdatedAt;
        }

        await using (var context = CreateContext(dbName))
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                CreateService(context).UpdateStatusAsync(id,
                    new UpdateRequestStatusDto { Status = to, RowVersion = rowVersion }));
        }

        await using var verify = CreateContext(dbName);
        var entity = await verify.Requests.SingleAsync(r => r.Id == id);
        Assert.Equal(from, entity.Status);
        Assert.Equal(updatedAtBefore, entity.UpdatedAt);
        Assert.Equal(rowVersion, Convert.ToBase64String(entity.RowVersion));
        Assert.Equal(0, await verify.RequestStatusHistory.CountAsync(h => h.RequestId == id));
    }

    [Fact]
    public void AllowedNextStatuses_CannotBeMutatedThroughTheSharedInstance()
    {
        var allowed = StatusTransitionPolicy.GetAllowedNextStatuses(RequestStatus.New);
        Assert.False(allowed is RequestStatus[]);
        Assert.Throws<NotSupportedException>(() => ((IList<RequestStatus>)allowed)[0] = RequestStatus.Completed);
    }

    [Fact]
    public async Task ListedRequests_CarryTheirAllowedNextStatuses_AndAnUpdateReturnsTheNewOnes()
    {
        var (dbName, id, rowVersion) = await SeedRequestAsync(RequestStatus.New);
        await using (var seed = CreateContext(dbName))
        {
            seed.Requests.Add(NewRequest("Done", "Org", RequestStatus.Completed, RequestPriority.Low, DateTime.UtcNow));
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateService(context);

        var listed = (await service.GetRequestsAsync(new RequestQueryDto { SortBy = "createdAt", SortDirection = "asc" })).Items;
        Assert.Equal([RequestStatus.InProgress], listed.Single(r => r.Id == id).AllowedNextStatuses);
        Assert.Empty(listed.Single(r => r.Status == RequestStatus.Completed).AllowedNextStatuses);

        var updated = await service.UpdateStatusAsync(id, new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion });
        Assert.Equal([RequestStatus.Waiting, RequestStatus.Completed], updated.AllowedNextStatuses);
    }

    [Fact]
    public async Task UpdateStatusAsync_MissingRequest_Returns404EvenForForbiddenTarget()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(context).UpdateStatusAsync(999,
                new UpdateRequestStatusDto { Status = RequestStatus.New, RowVersion = Convert.ToBase64String(new byte[8]) }));
    }

    [Fact]
    public async Task UpdateStatusAsync_StaleRowVersion_Returns409NotTransitionError()
    {
        // The client last saw New and asks for InProgress (valid from its point of view), but
        // another user has since completed the request. The answer must be "reload" (409),
        // not "Completed -> InProgress is forbidden" (400) about a state the client never saw.
        var (dbName, id, staleRowVersion) = await SeedRequestAsync(RequestStatus.New);
        await using (var other = CreateContext(dbName))
        {
            var entity = await other.Requests.SingleAsync(r => r.Id == id);
            entity.Status = RequestStatus.Completed;
            entity.RowVersion = BitConverter.GetBytes(2L);
            await other.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            CreateService(context).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = staleRowVersion }));
    }

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
