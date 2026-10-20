using System.Net;
using System.Net.Http.Json;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using static RequestsManagement.IntegrationTests.SqlServerApiFixture;

namespace RequestsManagement.IntegrationTests;

/// <summary>
/// T8.6: every status change is audited and readable through the history endpoint.
/// T8.7: the cached summary is fresh right after a change, through the real app's singleton cache.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class HistoryAndCacheApiTests(SqlServerApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EachChange_IsRecorded_AndHistoryReturnsThemNewestFirst()
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];
        var rowVersion = Convert.ToBase64String(request.RowVersion);
        var users = new[] { "דנה כהן", "יוסי לוי", "מיכל אברהם" };
        var path = new[] { "InProgress", "Waiting", "Completed" };

        for (var i = 0; i < path.Length; i++)
        {
            var response = await fixture.CreateClient(users[i]).PatchAsJsonAsync($"/api/requests/{request.Id}/status",
                new { status = path[i], rowVersion }, Json);
            rowVersion = (await response.Content.ReadFromJsonAsync<RequestDto>(Json))!.RowVersion;
        }

        var history = await fixture.CreateClient().GetFromJsonAsync<List<RequestStatusHistoryDto>>(
            $"/api/requests/{request.Id}/history", Json);

        Assert.Equal(
            [
                (RequestStatus.Waiting, RequestStatus.Completed, "מיכל אברהם"),
                (RequestStatus.InProgress, RequestStatus.Waiting, "יוסי לוי"),
                (RequestStatus.New, RequestStatus.InProgress, "דנה כהן")
            ],
            history!.Select(h => (h.PreviousStatus, h.NewStatus, h.ChangedBy)));
        Assert.True(history[0].ChangedAt >= history[1].ChangedAt && history[1].ChangedAt >= history[2].ChangedAt);

        // ChangedAt is the same instant as the request's UpdatedAt for the latest change.
        var current = await fixture.CreateClient().GetFromJsonAsync<DTOs.Common.PagedResultDto<RequestDto>>("/api/requests", Json);
        Assert.Equal(current!.Items.Single().UpdatedAt, history[0].ChangedAt);
    }

    [Fact]
    public async Task RequestWithoutChanges_HasEmptyHistory()
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];

        var response = await fixture.CreateClient().GetAsync($"/api/requests/{request.Id}/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<List<RequestStatusHistoryDto>>(Json))!);
    }

    [Fact]
    public async Task Summary_IsFreshRightAfterSingleAndBulkUpdates()
    {
        var seeded = await fixture.SeedAsync(Request("a"), Request("b"), Request("c"));
        var client = fixture.CreateClient();

        var before = await client.GetFromJsonAsync<RequestSummaryDto>("/api/requests/summary", Json);
        Assert.Equal((3, 3, 0), (before!.Total, before.ByStatus["New"], before.ByStatus["InProgress"]));
        // A second read is served from the cache; it must not mask the change that follows.
        await client.GetFromJsonAsync<RequestSummaryDto>("/api/requests/summary", Json);

        await client.PatchAsJsonAsync($"/api/requests/{seeded[0].Id}/status",
            new { status = "InProgress", rowVersion = Convert.ToBase64String(seeded[0].RowVersion) }, Json);
        var afterSingle = await client.GetFromJsonAsync<RequestSummaryDto>("/api/requests/summary", Json);
        Assert.Equal((2, 1), (afterSingle!.ByStatus["New"], afterSingle.ByStatus["InProgress"]));

        await client.PostAsJsonAsync("/api/requests/status/bulk", new
        {
            status = "InProgress",
            items = seeded.Skip(1).Select(r => new { id = r.Id, rowVersion = Convert.ToBase64String(r.RowVersion) })
        }, Json);
        var afterBulk = await client.GetFromJsonAsync<RequestSummaryDto>("/api/requests/summary", Json);
        Assert.Equal((0, 3), (afterBulk!.ByStatus["New"], afterBulk.ByStatus["InProgress"]));
    }
}
