using Microsoft.EntityFrameworkCore;
using RequestsManagement.Data;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Models;
using RequestsManagement.Services;

namespace RequestsManagement.Tests;

internal static class TestData
{
    public static AppDbContext CreateContext(string dbName) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options);

    public const string TestUser = "test-user";

    public static RequestService CreateService(AppDbContext context, ICurrentUser? currentUser = null) =>
        new(context, currentUser ?? new StubCurrentUser(TestUser));

    // Header parsing is tested separately (HeaderCurrentUserTests); service tests only need a user.
    public sealed class StubCurrentUser(string userName) : ICurrentUser
    {
        public string UserName => userName;
    }

    // Behaves like HeaderCurrentUser when the header is missing.
    public sealed class MissingCurrentUser : ICurrentUser
    {
        public string UserName => throw new ValidationException("The X-User-Name header is required for changes.");
    }

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
