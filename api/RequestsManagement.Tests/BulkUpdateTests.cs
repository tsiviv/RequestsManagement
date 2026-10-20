using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using Xunit;
using static RequestsManagement.Tests.TestData;

namespace RequestsManagement.Tests;

public class BulkUpdateTests
{
    private static readonly string StaleRowVersion = Convert.ToBase64String(BitConverter.GetBytes(99L));

    // --- Partial success -------------------------------------------------------

    [Fact]
    public async Task MixedOutcomes_EachRequestReportedInInputOrder_OnlyValidOneChanged()
    {
        var (dbName, seeded) = await SeedAsync(RequestStatus.New, RequestStatus.New, RequestStatus.Completed);
        var (valid, stale, finalStatus) = (seeded[0], seeded[1], seeded[2]);

        BulkUpdateStatusResultDto result;
        await using (var context = CreateContext(dbName))
        {
            result = await CreateService(context).BulkUpdateStatusAsync(new BulkUpdateStatusDto
            {
                Status = RequestStatus.InProgress,
                Items =
                [
                    new() { Id = finalStatus.Id, RowVersion = finalStatus.RowVersion },
                    new() { Id = 999, RowVersion = valid.RowVersion },
                    new() { Id = valid.Id, RowVersion = valid.RowVersion },
                    new() { Id = stale.Id, RowVersion = StaleRowVersion }
                ]
            });
        }

        Assert.Equal((4, 1, 3), (result.Requested, result.Updated, result.Failed));
        Assert.Equal([finalStatus.Id, 999, valid.Id, stale.Id], result.Results.Select(r => r.Id));
        Assert.Equal(
            [BulkUpdateOutcome.InvalidTransition, BulkUpdateOutcome.NotFound, BulkUpdateOutcome.Updated, BulkUpdateOutcome.Conflict],
            result.Results.Select(r => r.Outcome));

        var updated = result.Results[2];
        Assert.Null(updated.Error);
        Assert.Equal(RequestStatus.InProgress, updated.Request!.Status);
        Assert.All(result.Results.Where(r => r.Outcome != BulkUpdateOutcome.Updated), r =>
        {
            Assert.Null(r.Request);
            Assert.False(string.IsNullOrEmpty(r.Error));
        });
        Assert.Contains("סטטוס סופי", result.Results[0].Error);

        await using var verify = CreateContext(dbName);
        var statuses = await verify.Requests.ToDictionaryAsync(r => r.Id, r => r.Status);
        Assert.Equal(RequestStatus.InProgress, statuses[valid.Id]);
        Assert.Equal(RequestStatus.New, statuses[stale.Id]);
        Assert.Equal(RequestStatus.Completed, statuses[finalStatus.Id]);
        var history = Assert.Single(await verify.RequestStatusHistory.ToListAsync());
        Assert.Equal((valid.Id, RequestStatus.New, RequestStatus.InProgress, TestUser),
            (history.RequestId, history.PreviousStatus, history.NewStatus, history.ChangedBy));
    }

    [Fact]
    public async Task AllValid_UpdatesEveryRequest_WithOneHistoryRowEach()
    {
        var (dbName, seeded) = await SeedAsync(RequestStatus.InProgress, RequestStatus.Waiting, RequestStatus.InProgress);

        BulkUpdateStatusResultDto result;
        await using (var context = CreateContext(dbName))
        {
            result = await CreateService(context).BulkUpdateStatusAsync(new BulkUpdateStatusDto
            {
                Status = RequestStatus.Completed,
                Items = seeded.Select(s => new BulkUpdateStatusItemDto { Id = s.Id, RowVersion = s.RowVersion }).ToList()
            });
        }

        Assert.Equal((3, 3, 0), (result.Requested, result.Updated, result.Failed));
        await using var verify = CreateContext(dbName);
        Assert.All(await verify.Requests.ToListAsync(), r => Assert.Equal(RequestStatus.Completed, r.Status));
        Assert.Equal(3, await verify.RequestStatusHistory.CountAsync(h => h.NewStatus == RequestStatus.Completed));
    }

    [Fact]
    public async Task SameStatus_IsReportedAsInvalidTransition()
    {
        var (dbName, seeded) = await SeedAsync(RequestStatus.Waiting);

        await using var context = CreateContext(dbName);
        var result = await CreateService(context).BulkUpdateStatusAsync(new BulkUpdateStatusDto
        {
            Status = RequestStatus.Waiting,
            Items = [new() { Id = seeded[0].Id, RowVersion = seeded[0].RowVersion }]
        });

        var item = Assert.Single(result.Results);
        Assert.Equal(BulkUpdateOutcome.InvalidTransition, item.Outcome);
        Assert.Contains("כבר בסטטוס 'ממתין'", item.Error);
    }

    [Fact]
    public async Task ConflictDetectedAtSave_DoesNotLeakIntoTheNextItem()
    {
        // Item 1 passes the early check, but another user commits before our SaveChanges, so the
        // database rejects it. Its rolled-back changes must not be retried by item 2's SaveChanges,
        // otherwise item 2 would fail too.
        var (dbName, seeded) = await SeedAsync(RequestStatus.New, RequestStatus.New);
        var concurrentWriter = new ConcurrentWriterOnFirstSave(dbName, seeded[0].Id);

        BulkUpdateStatusResultDto result;
        await using (var context = CreateContext(dbName, concurrentWriter))
        {
            result = await CreateService(context).BulkUpdateStatusAsync(new BulkUpdateStatusDto
            {
                Status = RequestStatus.InProgress,
                Items = seeded.Select(s => new BulkUpdateStatusItemDto { Id = s.Id, RowVersion = s.RowVersion }).ToList()
            });
        }

        Assert.True(concurrentWriter.Fired);
        Assert.Equal([BulkUpdateOutcome.Conflict, BulkUpdateOutcome.Updated], result.Results.Select(r => r.Outcome));
        await using var verify = CreateContext(dbName);
        Assert.Equal(RequestStatus.Waiting, (await verify.Requests.SingleAsync(r => r.Id == seeded[0].Id)).Status); // the other user's change
        Assert.Equal(RequestStatus.InProgress, (await verify.Requests.SingleAsync(r => r.Id == seeded[1].Id)).Status);
        // (Rollback of item 1's audit row relies on the SQL Server transaction; see WORK_PLAN T8.3.)
    }

    // --- Request-level validation: 400, nothing updated ------------------------

    public static TheoryData<string, Func<SeededRequest[], BulkUpdateStatusDto>> InvalidRequests() => new()
    {
        { "duplicate ids", s => new() { Status = RequestStatus.InProgress, Items = [Item(s[0]), Item(s[1]), Item(s[0])] } },
        { "one malformed rowVersion", s => new() { Status = RequestStatus.InProgress, Items = [Item(s[0]), new() { Id = s[1].Id, RowVersion = "not base64!" }] } },
        { "missing status", s => new() { Status = null, Items = [Item(s[0])] } },
        { "undefined status", s => new() { Status = (RequestStatus)42, Items = [Item(s[0])] } },
        { "no items", s => new() { Status = RequestStatus.InProgress, Items = [] } },
        { "101 items", s => new() { Status = RequestStatus.InProgress, Items = Enumerable.Range(1, 101).Select(i => new BulkUpdateStatusItemDto { Id = i, RowVersion = s[0].RowVersion }).ToList() } }
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequest_ThrowsValidation_AndUpdatesNothing(string _, Func<SeededRequest[], BulkUpdateStatusDto> build)
    {
        var (dbName, seeded) = await SeedAsync(RequestStatus.New, RequestStatus.New);

        await using (var context = CreateContext(dbName))
        {
            await Assert.ThrowsAsync<ValidationException>(() => CreateService(context).BulkUpdateStatusAsync(build(seeded)));
        }

        await AssertNothingChangedAsync(dbName);
    }

    [Fact]
    public async Task MissingUser_ThrowsValidation_AndUpdatesNothing()
    {
        var (dbName, seeded) = await SeedAsync(RequestStatus.New);

        await using (var context = CreateContext(dbName))
        {
            await Assert.ThrowsAsync<ValidationException>(() => CreateService(context, new MissingCurrentUser())
                .BulkUpdateStatusAsync(new BulkUpdateStatusDto { Status = RequestStatus.InProgress, Items = [Item(seeded[0])] }));
        }

        await AssertNothingChangedAsync(dbName);
    }

    // --- Single update: same "missing status" fix as the bulk DTO --------------

    [Fact]
    public async Task SingleUpdate_MissingStatus_ThrowsValidation_RatherThanDefaultingToNew()
    {
        var (dbName, seeded) = await SeedAsync(RequestStatus.InProgress);

        await using var context = CreateContext(dbName);
        var ex = await Assert.ThrowsAsync<ValidationException>(() => CreateService(context)
            .UpdateStatusAsync(seeded[0].Id, new UpdateRequestStatusDto { Status = null, RowVersion = seeded[0].RowVersion }));
        Assert.Equal("יש לציין סטטוס.", ex.Message);
    }

    // --- Helpers -----------------------------------------------------------------

    public record SeededRequest(int Id, string RowVersion);

    private static BulkUpdateStatusItemDto Item(SeededRequest s) => new() { Id = s.Id, RowVersion = s.RowVersion };

    private static async Task<(string DbName, SeededRequest[] Seeded)> SeedAsync(params RequestStatus[] statuses)
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var entities = statuses.Select((s, i) => NewRequest($"R{i}", "Org", s, RequestPriority.Low, DateTime.UtcNow.AddDays(-1))).ToList();
        context.Requests.AddRange(entities);
        await context.SaveChangesAsync();
        return (dbName, entities.Select(e => new SeededRequest(e.Id, Convert.ToBase64String(e.RowVersion))).ToArray());
    }

    private static async Task AssertNothingChangedAsync(string dbName)
    {
        await using var verify = CreateContext(dbName);
        Assert.All(await verify.Requests.ToListAsync(), r => Assert.Equal(r.CreatedAt, r.UpdatedAt));
        Assert.Empty(await verify.RequestStatusHistory.ToListAsync());
    }

    // Simulates a second user whose update commits between our load and our first SaveChanges.
    private sealed class ConcurrentWriterOnFirstSave(string dbName, int requestId) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired)
            {
                Fired = true;
                await using var other = CreateContext(dbName);
                var entity = await other.Requests.SingleAsync(r => r.Id == requestId, cancellationToken);
                entity.Status = RequestStatus.Waiting;
                entity.RowVersion = BitConverter.GetBytes(2L); // SQL Server would bump this itself
                await other.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }
}
