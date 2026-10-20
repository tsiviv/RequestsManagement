using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Services;
using static RequestsManagement.IntegrationTests.SqlServerApiFixture;

namespace RequestsManagement.IntegrationTests;

/// <summary>
/// T8.3: optimistic concurrency enforced by SQL Server's rowversion, not by the client or the
/// InMemory provider: exactly one of several concurrent updates wins, the rest get 409, and a
/// conflict detected by the database rolls back the audit row with the update.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ConcurrencyApiTests(SqlServerApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SecondUpdateWithTheSameRowVersion_Gets409_AndDoesNotOverwriteTheFirst()
    {
        var request = (await fixture.SeedAsync(Request("r", RequestStatus.InProgress)))[0];
        var rowVersion = Convert.ToBase64String(request.RowVersion);
        var client = fixture.CreateClient();

        var first = await client.PatchAsJsonAsync($"/api/requests/{request.Id}/status",
            new { status = "Waiting", rowVersion }, Json);
        var second = await client.PatchAsJsonAsync($"/api/requests/{request.Id}/status",
            new { status = "Completed", rowVersion }, Json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var updated = await first.Content.ReadFromJsonAsync<RequestDto>(Json);
        Assert.NotEqual(rowVersion, updated!.RowVersion); // SQL Server bumped it

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>(Json);
        Assert.Equal(409, problem!.Status);

        await using var db = fixture.CreateDbContext();
        var stored = await db.Requests.SingleAsync(r => r.Id == request.Id);
        Assert.Equal(RequestStatus.Waiting, stored.Status); // no lost update
        Assert.Equal(updated.RowVersion, Convert.ToBase64String(stored.RowVersion));
        Assert.Equal(updated.UpdatedAt, stored.UpdatedAt);
        Assert.Single(await db.RequestStatusHistory.Where(h => h.RequestId == request.Id).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentUpdatesWithTheSameRowVersion_ExactlyOneSucceeds()
    {
        // A real race: all requests are in flight at once and may all pass the service's early
        // RowVersion check; only the database's WHERE RowVersion = @rv lets exactly one through.
        const int contenders = 8;
        var request = (await fixture.SeedAsync(Request("r", RequestStatus.InProgress)))[0];
        var rowVersion = Convert.ToBase64String(request.RowVersion);
        var targets = new[] { "Waiting", "Completed" };

        using var start = new SemaphoreSlim(0);
        var attempts = Enumerable.Range(0, contenders).Select(async i =>
        {
            var client = fixture.CreateClient($"user-{i}");
            await start.WaitAsync();
            var response = await client.PatchAsJsonAsync($"/api/requests/{request.Id}/status",
                new { status = targets[i % 2], rowVersion }, Json);
            return (User: $"user-{i}", response.StatusCode);
        }).ToList();
        start.Release(contenders);
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(contenders - 1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        // Exactly one change was recorded, by the winner, and the row reflects it.
        await using var db = fixture.CreateDbContext();
        var history = Assert.Single(await db.RequestStatusHistory.Where(h => h.RequestId == request.Id).ToListAsync());
        Assert.Equal(results.Single(r => r.StatusCode == HttpStatusCode.OK).User, history.ChangedBy);
        Assert.Equal(history.NewStatus, (await db.Requests.SingleAsync(r => r.Id == request.Id)).Status);
    }

    [Fact]
    public async Task ConflictDetectedOnlyAtSave_RollsBackTheAuditRowWithTheUpdate()
    {
        // Moved here from the unit tests (T3.3): it depends on the SaveChanges transaction, which
        // InMemory does not have. Another user commits between our load and our SaveChanges, so
        // the early check passes but the database rejects the UPDATE; the history INSERT in the
        // same transaction must not survive.
        var request = (await fixture.SeedAsync(Request("r", RequestStatus.InProgress)))[0];
        var concurrentWriter = new ConcurrentWriterOnFirstSave(fixture, request.Id);

        await using (var context = fixture.CreateDbContext(concurrentWriter))
        {
            var service = new RequestService(context, new FixedUser("late-user"), fixture.SummaryCache, NullLogger<RequestService>.Instance);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.UpdateStatusAsync(request.Id,
                new UpdateRequestStatusDto { Status = RequestStatus.Completed, RowVersion = Convert.ToBase64String(request.RowVersion) }));
        }

        Assert.True(concurrentWriter.Fired);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(RequestStatus.Waiting, (await db.Requests.SingleAsync(r => r.Id == request.Id)).Status); // the other user's change
        Assert.Empty(await db.RequestStatusHistory.Where(h => h.ChangedBy == "late-user").ToListAsync());
    }

    private sealed class FixedUser(string name) : ICurrentUser
    {
        public string UserName => name;
    }

    /// <summary>Commits another user's change right before our first SaveChanges.</summary>
    private sealed class ConcurrentWriterOnFirstSave(SqlServerApiFixture fixture, int requestId) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired)
            {
                Fired = true;
                await using var other = fixture.CreateDbContext();
                var entity = await other.Requests.SingleAsync(r => r.Id == requestId, cancellationToken);
                entity.Status = RequestStatus.Waiting; // SQL Server bumps the rowversion itself
                await other.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
    }
}
