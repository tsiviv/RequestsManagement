using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using static RequestsManagement.IntegrationTests.SqlServerApiFixture;

namespace RequestsManagement.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class BulkApiTests(SqlServerApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task MixedBatch_UpdatesTheValidOnes_AndReportsEveryOutcomeInOrder()
    {
        var seeded = await fixture.SeedAsync(
            Request("valid-1", RequestStatus.InProgress),
            Request("final", RequestStatus.Completed),
            Request("stale", RequestStatus.Waiting),
            Request("valid-2", RequestStatus.Waiting));
        var (valid1, final, stale, valid2) = (seeded[0], seeded[1], seeded[2], seeded[3]);

        var response = await fixture.CreateClient("דנה כהן").PostAsJsonAsync("/api/requests/status/bulk", new
        {
            status = "Completed",
            items = new object[]
            {
                new { id = valid1.Id, rowVersion = Convert.ToBase64String(valid1.RowVersion) },
                new { id = final.Id, rowVersion = Convert.ToBase64String(final.RowVersion) },
                new { id = 987654, rowVersion = Convert.ToBase64String(valid1.RowVersion) },
                new { id = stale.Id, rowVersion = "AAAAAAAAAAE=" },
                new { id = valid2.Id, rowVersion = Convert.ToBase64String(valid2.RowVersion) }
            }
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkUpdateStatusResultDto>(Json);
        Assert.Equal((5, 2, 3), (result!.Requested, result.Updated, result.Failed));
        Assert.Equal(
            [BulkUpdateOutcome.Updated, BulkUpdateOutcome.InvalidTransition, BulkUpdateOutcome.NotFound, BulkUpdateOutcome.Conflict, BulkUpdateOutcome.Updated],
            result.Results.Select(r => r.Outcome));

        var updated = result.Results.Where(r => r.Outcome == BulkUpdateOutcome.Updated).Select(r => r.Request!).ToList();
        Assert.All(updated, r => Assert.Equal(RequestStatus.Completed, r.Status));

        await using var db = fixture.CreateDbContext();
        var stored = await db.Requests.ToDictionaryAsync(r => r.Id);
        Assert.Equal(RequestStatus.Completed, stored[valid1.Id].Status);
        Assert.Equal(RequestStatus.Completed, stored[valid2.Id].Status);
        Assert.Equal(RequestStatus.Waiting, stored[stale.Id].Status);
        Assert.Equal(stale.RowVersion, stored[stale.Id].RowVersion);
        Assert.Equal(updated.Single(r => r.Id == valid1.Id).RowVersion, Convert.ToBase64String(stored[valid1.Id].RowVersion));

        var history = await db.RequestStatusHistory.OrderBy(h => h.RequestId).ToListAsync();
        Assert.Equal([valid1.Id, valid2.Id], history.Select(h => h.RequestId));
        Assert.All(history, h => Assert.Equal("דנה כהן", h.ChangedBy));
    }

    [Fact]
    public async Task OneHundredItems_AllUpdated()
    {
        var seeded = await fixture.SeedAsync(Enumerable.Range(0, 100).Select(i => Request($"r{i}")).ToArray());

        var response = await fixture.CreateClient().PostAsJsonAsync("/api/requests/status/bulk", new
        {
            status = "InProgress",
            items = seeded.Select(r => new { id = r.Id, rowVersion = Convert.ToBase64String(r.RowVersion) })
        }, Json);

        var result = await response.Content.ReadFromJsonAsync<BulkUpdateStatusResultDto>(Json);
        Assert.Equal((100, 100, 0), (result!.Requested, result.Updated, result.Failed));
        await using var db = fixture.CreateDbContext();
        Assert.Equal(100, await db.Requests.CountAsync(r => r.Status == RequestStatus.InProgress));
        Assert.Equal(100, await db.RequestStatusHistory.CountAsync());
    }
}
