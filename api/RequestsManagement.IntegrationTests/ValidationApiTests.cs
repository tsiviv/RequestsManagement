using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Enums;
using static RequestsManagement.IntegrationTests.SqlServerApiFixture;

namespace RequestsManagement.IntegrationTests;

/// <summary>
/// T8.4: invalid input is rejected through the real pipeline (model binding, [ApiController]
/// validation, the exception middleware) with 400 ProblemDetails, and changes nothing.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ValidationApiTests(SqlServerApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/api/requests?pageSize=101", "PageSize")]
    [InlineData("/api/requests?page=0", "Page")]
    [InlineData("/api/requests?createdFrom=2026-08-01&createdTo=2026-07-01", "CreatedFrom must be earlier than CreatedTo")]
    [InlineData("/api/requests?createdFrom=not-a-date", "CreatedFrom")]
    [InlineData("/api/requests?sortBy=title", "Invalid sortBy")]
    [InlineData("/api/requests?sortDirection=sideways", "Invalid sortDirection")]
    [InlineData("/api/requests?status=Archived", "status")]
    public async Task InvalidQuery_Returns400ProblemDetails(string url, string expectedText)
    {
        var response = await fixture.CreateClient().GetAsync(url);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, expectedText);
    }

    [Fact]
    public async Task ForbiddenTransition_Returns400_AndChangesNothing()
    {
        var request = (await fixture.SeedAsync(Request("done", RequestStatus.Completed)))[0];

        var response = await fixture.CreateClient().PatchAsJsonAsync($"/api/requests/{request.Id}/status",
            new { status = "InProgress", rowVersion = Convert.ToBase64String(request.RowVersion) }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "'Completed' is a final status");
        await AssertUnchangedAsync(request.Id, RequestStatus.Completed, request.RowVersion);
    }

    [Fact]
    public async Task SameStatus_Returns400()
    {
        var request = (await fixture.SeedAsync(Request("r", RequestStatus.Waiting)))[0];

        var response = await fixture.CreateClient().PatchAsJsonAsync($"/api/requests/{request.Id}/status",
            new { status = "Waiting", rowVersion = Convert.ToBase64String(request.RowVersion) }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "already in status 'Waiting'");
        await AssertUnchangedAsync(request.Id, RequestStatus.Waiting, request.RowVersion);
    }

    [Theory]
    [InlineData("""{ "rowVersion": "AAAAAAAAB9E=" }""", "Status is required")]
    [InlineData("""{ "status": "Archived", "rowVersion": "AAAAAAAAB9E=" }""", "status")]
    [InlineData("""{ "status": "InProgress", "rowVersion": "not base64!" }""", "base64")]
    public async Task MalformedUpdateBody_Returns400_AndChangesNothing(string body, string expectedText)
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];

        var response = await fixture.CreateClient().PatchAsync($"/api/requests/{request.Id}/status",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, expectedText);
        await AssertUnchangedAsync(request.Id, RequestStatus.New, request.RowVersion);
    }

    [Fact]
    public async Task MissingUserHeader_Returns400_AndChangesNothing()
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];

        var response = await fixture.CreateClient(userName: null).PatchAsJsonAsync($"/api/requests/{request.Id}/status",
            new { status = "InProgress", rowVersion = Convert.ToBase64String(request.RowVersion) }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "X-User-Name header is required");
        await AssertUnchangedAsync(request.Id, RequestStatus.New, request.RowVersion);
    }

    [Fact]
    public async Task UnknownRequest_Returns404_ForUpdateAndHistory()
    {
        var client = fixture.CreateClient();

        var update = await client.PatchAsJsonAsync("/api/requests/987654/status",
            new { status = "InProgress", rowVersion = "AAAAAAAAB9E=" }, Json);
        var history = await client.GetAsync("/api/requests/987654/history");

        await AssertProblemAsync(update, HttpStatusCode.NotFound, "987654");
        await AssertProblemAsync(history, HttpStatusCode.NotFound, "987654");
    }

    [Fact]
    public async Task BulkWithMoreThan100Items_Returns400_AndChangesNothing()
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];
        var items = Enumerable.Range(0, 101)
            .Select(i => new { id = request.Id + i, rowVersion = Convert.ToBase64String(request.RowVersion) });

        var response = await fixture.CreateClient().PostAsJsonAsync("/api/requests/status/bulk",
            new { status = "InProgress", items }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "at most 100");
        await AssertUnchangedAsync(request.Id, RequestStatus.New, request.RowVersion);
    }

    [Fact]
    public async Task BulkWithDuplicateIds_Returns400_AndChangesNothing()
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];
        var item = new { id = request.Id, rowVersion = Convert.ToBase64String(request.RowVersion) };

        var response = await fixture.CreateClient().PostAsJsonAsync("/api/requests/status/bulk",
            new { status = "InProgress", items = new[] { item, item } }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Duplicate ids");
        await AssertUnchangedAsync(request.Id, RequestStatus.New, request.RowVersion);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected, string expectedText)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        var problem = System.Text.Json.JsonSerializer.Deserialize<ValidationProblemDetails>(body, Json);
        Assert.Equal((int)expected, problem!.Status);
        Assert.Contains(expectedText, body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task AssertUnchangedAsync(int id, RequestStatus status, byte[] rowVersion)
    {
        await using var db = fixture.CreateDbContext();
        var stored = await db.Requests.SingleAsync(r => r.Id == id);
        Assert.Equal(status, stored.Status);
        Assert.Equal(rowVersion, stored.RowVersion);
        Assert.Empty(await db.RequestStatusHistory.ToListAsync());
    }
}
