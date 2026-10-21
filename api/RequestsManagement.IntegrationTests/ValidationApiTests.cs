using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Enums;
using static RequestsManagement.IntegrationTests.SqlServerApiFixture;

namespace RequestsManagement.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class ValidationApiTests(SqlServerApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/api/requests?pageSize=101", "PageSize")]
    [InlineData("/api/requests?pageSize=0", "PageSize")]
    [InlineData("/api/requests?page=0", "Page")]
    [InlineData("/api/requests?createdFrom=2026-08-01&createdTo=2026-07-01", "חייב להיות מוקדם מתאריך הסיום")]
    [InlineData("/api/requests?createdFrom=not-a-date", "CreatedFrom")]
    [InlineData("/api/requests?sortBy=title", "שדה המיון")]
    [InlineData("/api/requests?sortDirection=sideways", "כיוון המיון")]
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

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "'הושלם' הוא סטטוס סופי");
        await AssertUnchangedAsync(request.Id, RequestStatus.Completed, request.RowVersion);
    }

    [Fact]
    public async Task SameStatus_Returns400()
    {
        var request = (await fixture.SeedAsync(Request("r", RequestStatus.Waiting)))[0];

        var response = await fixture.CreateClient().PatchAsJsonAsync($"/api/requests/{request.Id}/status",
            new { status = "Waiting", rowVersion = Convert.ToBase64String(request.RowVersion) }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "כבר בסטטוס 'ממתין'");
        await AssertUnchangedAsync(request.Id, RequestStatus.Waiting, request.RowVersion);
    }

    [Theory]
    [InlineData("""{ "rowVersion": "AAAAAAAAB9E=" }""", "יש לציין סטטוס")]
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

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "חסר שם משתמש");
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

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "עד 100 פניות");
        await AssertUnchangedAsync(request.Id, RequestStatus.New, request.RowVersion);
    }

    [Fact]
    public async Task BulkWithDuplicateIds_Returns400_AndChangesNothing()
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];
        var item = new { id = request.Id, rowVersion = Convert.ToBase64String(request.RowVersion) };

        var response = await fixture.CreateClient().PostAsJsonAsync("/api/requests/status/bulk",
            new { status = "InProgress", items = new[] { item, item } }, Json);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "מזהים כפולים");
        await AssertUnchangedAsync(request.Id, RequestStatus.New, request.RowVersion);
    }

    public static TheoryData<string, string, string, string?> ErrorRequests() => new()
    {
        // method, url, body, user header (null = none)
        { "GET", "/api/requests?status=Archived", "", DefaultUser },                 // model binding: invalid enum in query
        { "GET", "/api/requests?createdFrom=not-a-date", "", DefaultUser },          // model binding: invalid date
        { "GET", "/api/requests?pageSize=101", "", DefaultUser },                     // DataAnnotations
        { "GET", "/api/requests?page=abc", "", DefaultUser },                         // model binding: not a number
        { "GET", "/api/requests?sortBy=title", "", DefaultUser },                     // service validation
        { "PATCH", "/api/requests/{id}/status", "not json at all", DefaultUser },    // unreadable JSON
        { "PATCH", "/api/requests/{id}/status", """{ "status": "Archived", "rowVersion": "AAAAAAAAB9E=" }""", DefaultUser }, // bad enum in JSON
        { "PATCH", "/api/requests/{id}/status", "", DefaultUser },                    // empty body
        { "PATCH", "/api/requests/{id}/status", """{ "status": "InProgress", "rowVersion": "{rv}" }""", null }, // no user
        { "PATCH", "/api/requests/987654/status", """{ "status": "InProgress", "rowVersion": "AAAAAAAAB9E=" }""", DefaultUser }, // 404
        { "PATCH", "/api/requests/{id}/status", """{ "status": "InProgress", "rowVersion": "AAAAAAAAAAE=" }""", DefaultUser }, // 409
        { "PATCH", "/api/requests/{id}/status", """{ "status": "Completed", "rowVersion": "{rv}" }""", DefaultUser }, // forbidden transition
        { "POST", "/api/requests/status/bulk", """{ "status": "InProgress", "items": [] }""", DefaultUser } // bulk: no items
    };

    [Theory]
    [MemberData(nameof(ErrorRequests))]
    public async Task EveryErrorResponse_IsInHebrew(string method, string url, string body, string? user)
    {
        var request = (await fixture.SeedAsync(Request("r")))[0];
        var rowVersion = Convert.ToBase64String(request.RowVersion);
        var message = new HttpRequestMessage(new HttpMethod(method), url.Replace("{id}", request.Id.ToString()));
        if (method != "GET")
        {
            message.Content = new StringContent(body.Replace("{rv}", rowVersion), System.Text.Encoding.UTF8, "application/json");
        }

        var response = await fixture.CreateClient(user).SendAsync(message);

        Assert.True((int)response.StatusCode is >= 400 and < 500, $"status {(int)response.StatusCode}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = System.Text.Json.JsonSerializer.Deserialize<ValidationProblemDetails>(await response.Content.ReadAsStringAsync(), Json)!;
        var texts = new[] { problem.Title, problem.Detail }
            .Concat(problem.Errors.Values.SelectMany(v => v))
            .OfType<string>()
            .Where(t => t.Length > 0)
            .ToList();

        Assert.NotEmpty(texts);
        Assert.All(texts, t => Assert.Matches("[֐-׿]", t)); // every message has Hebrew text
        foreach (var english in new[] { "is not valid", "required", "One or more", "must be", "was not found", "could not be converted", "Cannot change" })
        {
            Assert.DoesNotContain(texts, t => t.Contains(english, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expected, string expectedText)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        var problem = System.Text.Json.JsonSerializer.Deserialize<ValidationProblemDetails>(body, Json);
        Assert.Equal((int)expected, problem!.Status);
        // Search the decoded fields, not the raw body: JSON may escape Hebrew as \uXXXX.
        var text = string.Join(" | ", new[] { problem.Title, problem.Detail }
            .Concat(problem.Errors.Keys)
            .Concat(problem.Errors.Values.SelectMany(v => v)));
        Assert.Contains(expectedText, text, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrEmpty(problem.Title));
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
