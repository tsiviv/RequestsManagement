using Microsoft.EntityFrameworkCore;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Services;
using Xunit;
using static RequestsManagement.Tests.TestData;

namespace RequestsManagement.Tests;

public class RequestServiceTests
{
    [Fact]
    public async Task GetRequestsAsync_FiltersByStatus_AndSortsAscending()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await using (var seedContext = CreateContext(dbName))
        {
            seedContext.Requests.AddRange(
                NewRequest("A", "Org1", RequestStatus.New, RequestPriority.Low, now.AddDays(-3)),
                NewRequest("B", "Org2", RequestStatus.New, RequestPriority.Medium, now.AddDays(-2)),
                NewRequest("C", "Org3", RequestStatus.Completed, RequestPriority.High, now.AddDays(-1)));
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateQueryService(context);

        var result = await service.GetRequestsAsync(new RequestQueryDto
        {
            Status = RequestStatus.New,
            Page = 1,
            PageSize = 10,
            SortBy = "createdAt",
            SortDirection = "asc"
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal(RequestStatus.New, i.Status));
        Assert.Equal("A", result.Items[0].Title);
        Assert.Equal("B", result.Items[1].Title);
    }

    [Fact]
    public async Task GetRequestsAsync_AppliesPagination()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await using (var seedContext = CreateContext(dbName))
        {
            for (var i = 0; i < 5; i++)
            {
                seedContext.Requests.Add(NewRequest($"Req{i}", "Org", RequestStatus.New, RequestPriority.Low, now.AddMinutes(i)));
            }
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateQueryService(context);

        var result = await service.GetRequestsAsync(new RequestQueryDto
        {
            Page = 2,
            PageSize = 2,
            SortBy = "createdAt",
            SortDirection = "asc"
        });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Req2", result.Items[0].Title);
        Assert.Equal("Req3", result.Items[1].Title);
    }

    [Fact]
    public async Task GetRequestsAsync_WithPageFarBeyondAvailableData_ReturnsEmptyPageInsteadOfOverflowing()
    {
        var dbName = Guid.NewGuid().ToString();

        await using (var seedContext = CreateContext(dbName))
        {
            seedContext.Requests.Add(NewRequest("A", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow));
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateQueryService(context);

        var result = await service.GetRequestsAsync(new RequestQueryDto
        {
            Page = int.MaxValue,
            PageSize = 100
        });

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(int.MaxValue, result.Page);
        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task GetRequestsAsync_CombinesOrganizationAssigneeAndHalfOpenDateRange()
    {
        var dbName = Guid.NewGuid().ToString();
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        await using (var seedContext = CreateContext(dbName))
        {
            seedContext.Requests.AddRange(
                NewRequest("Match-AtFrom", "Org1", RequestStatus.New, RequestPriority.Low, from, "Dana"),
                NewRequest("Match-Inside", "Org1", RequestStatus.New, RequestPriority.Low, from.AddDays(10), "Dana"),
                NewRequest("AtTo-Excluded", "Org1", RequestStatus.New, RequestPriority.Low, to, "Dana"),
                NewRequest("BeforeFrom", "Org1", RequestStatus.New, RequestPriority.Low, from.AddTicks(-1), "Dana"),
                NewRequest("OtherAssignee", "Org1", RequestStatus.New, RequestPriority.Low, from.AddDays(5), "Yossi"),
                // Exact organization match: a name that merely contains "Org1" must not match.
                NewRequest("OtherOrg", "Org10", RequestStatus.New, RequestPriority.Low, from.AddDays(5), "Dana"));
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateQueryService(context);

        var result = await service.GetRequestsAsync(new RequestQueryDto
        {
            OrganizationName = " Org1 ",
            AssignedTo = "Dana",
            CreatedFrom = from,
            CreatedTo = to,
            SortBy = "createdAt",
            SortDirection = "asc"
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(["Match-AtFrom", "Match-Inside"], result.Items.Select(i => i.Title));
        Assert.All(result.Items, i => Assert.Equal("Dana", i.AssignedTo));
    }

    [Theory]
    [InlineData(0)]   // from == to: the half-open range would be empty
    [InlineData(-1)]  // from after to
    public async Task GetRequestsAsync_WithCreatedFromNotBeforeCreatedTo_ThrowsValidationException(int daysFromToFrom)
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateQueryService(context);
        var from = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetRequestsAsync(new RequestQueryDto { CreatedFrom = from, CreatedTo = from.AddDays(daysFromToFrom) }));
    }

    [Fact]
    public async Task GetRequestsAsync_SortingByNonUniqueField_PagesDeterministicallyById()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await using (var seedContext = CreateContext(dbName))
        {
            // All rows share the same organization and creation time, so only the Id tiebreaker defines their order.
            for (var i = 0; i < 6; i++)
            {
                seedContext.Requests.Add(NewRequest($"Req{i}", "Org", RequestStatus.Waiting, RequestPriority.Low, now, "Dana"));
            }
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateQueryService(context);

        var pages = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await service.GetRequestsAsync(new RequestQueryDto
            {
                Page = page,
                PageSize = 2,
                SortBy = "organizationName",
                SortDirection = "desc"
            });
            pages.AddRange(result.Items.Select(i => i.Id));
        }

        Assert.Equal(6, pages.Distinct().Count());
        Assert.Equal(pages.OrderByDescending(id => id), pages);
    }

    [Theory]
    [InlineData("asc", new[] { "Dana-old", "Yossi-old", "Yossi-new", "Dana-new" })]
    [InlineData("desc", new[] { "Dana-new", "Yossi-new", "Yossi-old", "Dana-old" })]
    public async Task SortingByOrganization_GroupsThem_ThenOrdersByCreationInTheSameDirection(
        string direction, string[] expectedTitles)
    {
        var dbName = Guid.NewGuid().ToString();
        var day = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var seedContext = CreateContext(dbName))
        {
            seedContext.Requests.AddRange(
                NewRequest("Yossi-new", "Org B", RequestStatus.New, RequestPriority.Low, day.AddDays(2), "Yossi"),
                NewRequest("Dana-old", "Org A", RequestStatus.New, RequestPriority.Low, day, "Dana"),
                NewRequest("Yossi-old", "Org A", RequestStatus.New, RequestPriority.Low, day.AddDays(1), "Yossi"),
                NewRequest("Dana-new", "Org B", RequestStatus.New, RequestPriority.Low, day.AddDays(3), "Dana"));
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var result = await CreateQueryService(context).GetRequestsAsync(new RequestQueryDto { SortBy = "organizationName", SortDirection = direction });

        Assert.Equal(expectedTitles, result.Items.Select(i => i.Title));
    }

    [Theory]
    [InlineData("asc", new[] { "Low-old", "Low-new", "Medium", "High-old", "High-new" })]
    [InlineData("desc", new[] { "High-new", "High-old", "Medium", "Low-new", "Low-old" })]
    public async Task SortingByPriority_OrdersByUrgency_ThenByCreationInTheSameDirection(
        string direction, string[] expectedTitles)
    {
        var dbName = Guid.NewGuid().ToString();
        var day = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        await using (var seedContext = CreateContext(dbName))
        {
            // Inserted out of order, and with names that sort differently alphabetically
            // ("High" < "Low" < "Medium"), so only the enum's urgency order yields the expected list.
            seedContext.Requests.AddRange(
                NewRequest("Medium", "Org", RequestStatus.New, RequestPriority.Medium, day.AddDays(4)),
                NewRequest("High-old", "Org", RequestStatus.New, RequestPriority.High, day),
                NewRequest("Low-new", "Org", RequestStatus.New, RequestPriority.Low, day.AddDays(3)),
                NewRequest("High-new", "Org", RequestStatus.New, RequestPriority.High, day.AddDays(2)),
                NewRequest("Low-old", "Org", RequestStatus.New, RequestPriority.Low, day.AddDays(1)));
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var result = await CreateQueryService(context).GetRequestsAsync(new RequestQueryDto { SortBy = "priority", SortDirection = direction });

        Assert.Equal(expectedTitles, result.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task SortingByPriority_PagesDeterministicallyById()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        await using (var seedContext = CreateContext(dbName))
        {
            // Same priority and creation time: only the Id tiebreaker defines the order across pages.
            for (var i = 0; i < 6; i++)
            {
                seedContext.Requests.Add(NewRequest($"Req{i}", "Org", RequestStatus.New, RequestPriority.High, now));
            }
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateQueryService(context);

        var ids = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await service.GetRequestsAsync(new RequestQueryDto { Page = page, PageSize = 2, SortBy = "priority", SortDirection = "asc" });
            ids.AddRange(result.Items.Select(i => i.Id));
        }

        Assert.Equal(6, ids.Distinct().Count());
        Assert.Equal(ids.Order(), ids);
    }

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task SortingById_OrdersByIdentifier(string direction)
    {
        var dbName = Guid.NewGuid().ToString();
        await using (var seedContext = CreateContext(dbName))
        {
            for (var i = 0; i < 4; i++)
            {
                // Creation times deliberately out of Id order, so a date sort would differ.
                seedContext.Requests.Add(NewRequest($"R{i}", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow.AddDays(-i)));
            }
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var ids = (await CreateQueryService(context).GetRequestsAsync(new RequestQueryDto { SortBy = "id", SortDirection = direction }))
            .Items.Select(i => i.Id).ToList();

        Assert.Equal(direction == "asc" ? ids.Order() : ids.OrderDescending(), ids);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("assignedTo")]
    [InlineData("title")]
    public async Task SortingByAFieldThatIsNotSortable_IsRejected(string sortBy)
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateQueryService(context).GetRequestsAsync(new RequestQueryDto { SortBy = sortBy }));
        Assert.Contains("createdat, updatedat, organizationname, priority, id", ex.Message);
    }

    [Fact]
    public async Task GetFilterOptionsAsync_ReturnsDistinctSortedOrganizationsAndAssignees()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        await using (var seedContext = CreateContext(dbName))
        {
            seedContext.Requests.AddRange(
                NewRequest("A", "Org B", RequestStatus.New, RequestPriority.Low, now, "Yossi"),
                NewRequest("B", "Org A", RequestStatus.New, RequestPriority.Low, now, "Dana"),
                NewRequest("C", "Org B", RequestStatus.New, RequestPriority.Low, now, "Dana"));
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var options = await CreateQueryService(context).GetFilterOptionsAsync();

        Assert.Equal(["Org A", "Org B"], options.Organizations);
        Assert.Equal(["Dana", "Yossi"], options.Assignees);
    }

    [Fact]
    public async Task GetRequestsAsync_WithInvalidSortField_ThrowsValidationException()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateQueryService(context);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.GetRequestsAsync(new RequestQueryDto { SortBy = "'; DROP TABLE Requests;--" }));
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsCountsByStatusAndPriority()
    {
        var dbName = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await using (var seedContext = CreateContext(dbName))
        {
            seedContext.Requests.AddRange(
                NewRequest("A", "Org1", RequestStatus.New, RequestPriority.Low, now),
                NewRequest("B", "Org2", RequestStatus.New, RequestPriority.High, now),
                NewRequest("C", "Org3", RequestStatus.Completed, RequestPriority.High, now));
            await seedContext.SaveChangesAsync();
        }

        await using var context = CreateContext(dbName);
        var service = CreateQueryService(context);

        var summary = await service.GetSummaryAsync();

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.ByStatus[nameof(RequestStatus.New)]);
        Assert.Equal(1, summary.ByStatus[nameof(RequestStatus.Completed)]);
        Assert.Equal(2, summary.ByPriority[nameof(RequestPriority.High)]);
        Assert.Equal(1, summary.ByPriority[nameof(RequestPriority.Low)]);

        Assert.Equal(0, summary.ByStatus[nameof(RequestStatus.InProgress)]);
        Assert.Equal(0, summary.ByStatus[nameof(RequestStatus.Waiting)]);
        Assert.Equal(0, summary.ByPriority[nameof(RequestPriority.Medium)]);
        Assert.Equal(Enum.GetValues<RequestStatus>().Length, summary.ByStatus.Count);
        Assert.Equal(Enum.GetValues<RequestPriority>().Length, summary.ByPriority.Count);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithCurrentRowVersion_UpdatesStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        int requestId;
        string rowVersion;

        await using (var seedContext = CreateContext(dbName))
        {
            var entity = NewRequest("A", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow);
            seedContext.Requests.Add(entity);
            await seedContext.SaveChangesAsync();
            requestId = entity.Id;
            rowVersion = Convert.ToBase64String(entity.RowVersion);
        }

        await using var context = CreateContext(dbName);
        var service = CreateStatusService(context);

        var result = await service.UpdateStatusAsync(requestId,
            new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion });

        Assert.Equal(RequestStatus.InProgress, result.Status);
        Assert.Equal(requestId, result.Id);
        Assert.Equal("A", result.Title);
        Assert.Equal("Handler", result.AssignedTo);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenRequestDoesNotExist_ThrowsNotFoundException()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var service = CreateStatusService(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateStatusAsync(999, new UpdateRequestStatusDto
            {
                Status = RequestStatus.New,
                RowVersion = Convert.ToBase64String(new byte[8])
            }));
    }

    [Fact]
    public async Task UpdateStatusAsync_WithUndefinedStatus_ThrowsValidationException()
    {
        var dbName = Guid.NewGuid().ToString();
        int requestId;
        string rowVersion;

        await using (var seedContext = CreateContext(dbName))
        {
            var entity = NewRequest("A", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow);
            seedContext.Requests.Add(entity);
            await seedContext.SaveChangesAsync();
            requestId = entity.Id;
            rowVersion = Convert.ToBase64String(entity.RowVersion);
        }

        await using var context = CreateContext(dbName);
        var service = CreateStatusService(context);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStatusAsync(requestId, new UpdateRequestStatusDto
            {
                Status = (RequestStatus)999,
                RowVersion = rowVersion
            }));
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenRowVersionIsStale_ThrowsConcurrencyConflictException()
    {
        var dbName = Guid.NewGuid().ToString();
        int requestId;
        string originalRowVersion;

        await using (var seedContext = CreateContext(dbName))
        {
            var entity = NewRequest("A", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow);
            seedContext.Requests.Add(entity);
            await seedContext.SaveChangesAsync();
            requestId = entity.Id;
            originalRowVersion = Convert.ToBase64String(entity.RowVersion);
        }

        // Simulate a second client updating the request first, advancing RowVersion
        // (SQL Server would bump this automatically; InMemory requires it explicitly).
        await using (var otherClientContext = CreateContext(dbName))
        {
            var entity = await otherClientContext.Requests.SingleAsync(r => r.Id == requestId);
            entity.Status = RequestStatus.InProgress;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.RowVersion = BitConverter.GetBytes(2L);
            await otherClientContext.SaveChangesAsync();
        }

        await using var staleClientContext = CreateContext(dbName);
        var service = CreateStatusService(staleClientContext);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service.UpdateStatusAsync(requestId, new UpdateRequestStatusDto
            {
                Status = RequestStatus.Completed,
                RowVersion = originalRowVersion
            }));
    }
}
