using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.Enums;
using RequestsManagement.Models;

namespace RequestsManagement.Tests;

internal static class TestData
{
    public static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    public static RequestEntity NewRequest(string title, string org, RequestStatus status, RequestPriority priority, DateTime createdAt, string assignedTo = "Handler") =>
        new()
        {
            Title = title,
            OrganizationName = org,
            AssignedTo = assignedTo,
            Status = status,
            Priority = priority,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            // The InMemory provider does not auto-generate rowversion values the way
            // SQL Server does, so tests that exercise concurrency set this explicitly.
            RowVersion = BitConverter.GetBytes(1L)
        };
}
