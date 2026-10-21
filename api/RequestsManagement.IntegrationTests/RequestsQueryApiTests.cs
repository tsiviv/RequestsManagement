using System.Net;
using System.Net.Http.Json;
using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;
using RequestsManagement.Enums;
using static RequestsManagement.IntegrationTests.SqlServerApiFixture;

namespace RequestsManagement.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class RequestsQueryApiTests(SqlServerApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly DateTime July = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CombinedFilters_SortAndPaging_ReturnExactlyTheMatchingPage()
    {
        // 12 matching rows on consecutive days, plus rows that each miss exactly one filter.
        var matching = Enumerable.Range(0, 12)
            .Select(i => Request($"match-{i:00}", RequestStatus.InProgress, "רכבת ישראל - מטה", "דנה כהן", July.AddDays(i)))
            .ToArray();
        await fixture.SeedAsync([
            .. matching,
            Request("other-org", RequestStatus.InProgress, "רכבת ישראל - סניף חיפה", "דנה כהן", July.AddDays(3)),
            Request("other-handler", RequestStatus.InProgress, "רכבת ישראל - מטה", "יוסי לוי", July.AddDays(3)),
            Request("other-status", RequestStatus.Waiting, "רכבת ישראל - מטה", "דנה כהן", July.AddDays(3)),
            Request("before-range", RequestStatus.InProgress, "רכבת ישראל - מטה", "דנה כהן", July.AddDays(-1)),
            Request("at-range-end", RequestStatus.InProgress, "רכבת ישראל - מטה", "דנה כהן", July.AddMonths(1))
        ]);

        var org = Uri.EscapeDataString("רכבת ישראל - מטה");
        var handler = Uri.EscapeDataString("דנה כהן");
        var page = await fixture.CreateClient().GetFromJsonAsync<PagedResultDto<RequestDto>>(
            $"/api/requests?organizationName={org}&assignedTo={handler}&status=InProgress" +
            "&createdFrom=2026-07-01&createdTo=2026-08-01&sortBy=createdAt&sortDirection=desc&page=2&pageSize=5",
            Json);

        Assert.NotNull(page);
        Assert.Equal(12, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        // Newest first: page 1 = match-11..07, page 2 = match-06..02.
        Assert.Equal(["match-06", "match-05", "match-04", "match-03", "match-02"], page.Items.Select(i => i.Title));
        Assert.All(page.Items, i =>
        {
            Assert.Equal("דנה כהן", i.AssignedTo);
            Assert.Equal([RequestStatus.Waiting, RequestStatus.Completed], i.AllowedNextStatuses);
            Assert.False(string.IsNullOrEmpty(i.RowVersion));
        });
    }

    [Fact]
    public async Task PagingThroughEveryPage_ReturnsEachRowExactlyOnce()
    {
        // Same organization and creation time for all: only the Id tiebreaker orders them, which
        // must hold through the SQL (deferred join + OFFSET), not just in LINQ to objects.
        await fixture.SeedAsync(Enumerable.Range(0, 23).Select(i => Request($"r{i}", organization: "Same", createdAt: July)).ToArray());
        var client = fixture.CreateClient();

        var seen = new List<int>();
        for (var pageNumber = 1; pageNumber <= 5; pageNumber++)
        {
            var page = await client.GetFromJsonAsync<PagedResultDto<RequestDto>>(
                $"/api/requests?sortBy=organizationName&sortDirection=asc&page={pageNumber}&pageSize=5", Json);
            seen.AddRange(page!.Items.Select(i => i.Id));
        }

        Assert.Equal(23, seen.Count);
        Assert.Equal(23, seen.Distinct().Count());
        Assert.Equal(seen.Order(), seen);
    }

    [Fact]
    public async Task FreeTextSearch_MatchesTitleOrOrganization_AsSqlLike()
    {
        await fixture.SeedAsync(
            Request("תיקון חשבונית - ספק", organization: "בנק"),
            Request("בקשת גישה", organization: "חשבונית בע\"מ"),
            Request("100% דחוף", organization: "בנק"),
            Request("אחר", organization: "בנק"));
        var client = fixture.CreateClient();

        var byText = await client.GetFromJsonAsync<PagedResultDto<RequestDto>>(
            $"/api/requests?search={Uri.EscapeDataString("חשבונית")}", Json);
        // '%' typed by the user is a literal, not a LIKE wildcard (EF escapes it).
        var byPercent = await client.GetFromJsonAsync<PagedResultDto<RequestDto>>(
            $"/api/requests?search={Uri.EscapeDataString("0%")}", Json);

        Assert.Equal(2, byText!.TotalCount);
        Assert.Equal(["100% דחוף"], byPercent!.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task UnfilteredTotal_MatchesTheDatabase()
    {
        // The unfiltered total comes from the cached summary rather than a COUNT; it must still be right.
        await fixture.SeedAsync(Enumerable.Range(0, 7).Select(i => Request($"r{i}")).ToArray());

        var page = await fixture.CreateClient().GetFromJsonAsync<PagedResultDto<RequestDto>>("/api/requests?pageSize=3", Json);

        Assert.Equal(7, page!.TotalCount);
        Assert.Equal(3, page.Items.Count);
    }

    [Theory]
    [InlineData("/api/requests?sortBy=")]
    [InlineData("/api/requests?sortDirection=")]
    [InlineData("/api/requests?sortBy=&sortDirection=")]
    [InlineData("/api/requests?sortBy=%20%20")]
    public async Task EmptySortParameters_FallBackToTheDefaultOrder(string url)
    {
        // An empty query value binds as null (ConvertEmptyStringToNull), not as the DTO default, so
        // "?sortBy=" from a client that clears a field must still mean "default sort", never a 500.
        var day = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await fixture.SeedAsync(Request("older", createdAt: day), Request("newer", createdAt: day.AddDays(1)));

        var response = await fixture.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResultDto<RequestDto>>(Json);
        Assert.Equal(["newer", "older"], page!.Items.Select(i => i.Title)); // createdAt desc
    }

    [Fact]
    public async Task Swagger_DocumentsTheUserHeader_OnChangingOperationsOnly()
    {
        // Reviewers try the API from Swagger UI; without the header documented, every update is a 400.
        var json = await fixture.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var paths = doc.RootElement.GetProperty("paths");

        bool HasUserHeader(string path, string method) =>
            paths.GetProperty(path).GetProperty(method).TryGetProperty("parameters", out var ps) &&
            ps.EnumerateArray().Any(p => p.GetProperty("name").GetString() == "X-User-Name" && p.GetProperty("in").GetString() == "header");

        Assert.True(HasUserHeader("/api/requests/{id}/status", "patch"));
        Assert.True(HasUserHeader("/api/requests/status/bulk", "post"));
        Assert.False(HasUserHeader("/api/requests", "get"));
    }

    [Fact]
    public async Task FilterOptions_ListDistinctOrganizationsAndHandlers()
    {
        await fixture.SeedAsync(
            Request("a", organization: "ב", assignedTo: "יוסי"),
            Request("b", organization: "א", assignedTo: "דנה"),
            Request("c", organization: "ב", assignedTo: "דנה"));

        var response = await fixture.CreateClient().GetAsync("/api/requests/filter-options");
        var options = await response.Content.ReadFromJsonAsync<RequestFilterOptionsDto>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["א", "ב"], options!.Organizations);
        Assert.Equal(["דנה", "יוסי"], options.Assignees);
    }
}
