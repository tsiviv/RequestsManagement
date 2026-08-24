using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Models;
using RequestsManagement.Services;
using Xunit;

namespace RequestsManagement.Tests;

public class RequestServiceTests
{
    private static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    private static RequestEntity NewRequest(string title, string org, RequestStatus status, RequestPriority priority, DateTime createdAt) =>
        new()
        {
            Title = title,
            OrganizationName = org,
            Status = status,
            Priority = priority,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            // The InMemory provider does not auto-generate rowversion values the way
            // SQL Server does, so tests that exercise concurrency set this explicitly.
            RowVersion = BitConverter.GetBytes(1L)
        };

    // --- Query behavior ---------------------------------------------------

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
        var service = new RequestService(context);

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
        var service = new RequestService(context);

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
    public async Task GetRequestsAsync_WithInvalidSortField_ThrowsValidationException()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var service = new RequestService(context);

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
        var service = new RequestService(context);

        var summary = await service.GetSummaryAsync();

        Assert.Equal(3, summary.Total);
        Assert.Equal(2, summary.ByStatus[nameof(RequestStatus.New)]);
        Assert.Equal(1, summary.ByStatus[nameof(RequestStatus.Completed)]);
        Assert.Equal(2, summary.ByPriority[nameof(RequestPriority.High)]);
        Assert.Equal(1, summary.ByPriority[nameof(RequestPriority.Low)]);
    }

    // --- Status update behavior --------------------------------------------

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
        var service = new RequestService(context);

        var result = await service.UpdateStatusAsync(requestId,
            new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion });

        Assert.Equal(RequestStatus.InProgress, result.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenRequestDoesNotExist_ThrowsNotFoundException()
    {
        await using var context = CreateContext(Guid.NewGuid().ToString());
        var service = new RequestService(context);

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
        var service = new RequestService(context);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateStatusAsync(requestId, new UpdateRequestStatusDto
            {
                Status = (RequestStatus)999,
                RowVersion = rowVersion
            }));
    }

    // --- Optimistic concurrency (the selected assessment challenge) --------

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

        // First client attempts to update using the now-stale RowVersion it originally read.
        await using var staleClientContext = CreateContext(dbName);
        var service = new RequestService(staleClientContext);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            service.UpdateStatusAsync(requestId, new UpdateRequestStatusDto
            {
                Status = RequestStatus.Completed,
                RowVersion = originalRowVersion
            }));
    }
}
