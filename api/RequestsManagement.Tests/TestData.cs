using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RequestsManagement.Data;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Models;
using RequestsManagement.Services;

namespace RequestsManagement.Tests;

internal static class TestData
{
    public static AppDbContext CreateContext(string dbName, params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(interceptors)
            .Options);

    public const string TestUser = "test-user";

    public static RequestService CreateService(AppDbContext context, ICurrentUser? currentUser = null, ILogger<RequestService>? logger = null) =>
        new(context, currentUser ?? new StubCurrentUser(TestUser), logger ?? NullLogger<RequestService>.Instance);

    // Records log entries (level, rendered message, structured values) so tests can assert on them.
    public sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Values)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : new Dictionary<string, object?>();
            Entries.Add((logLevel, formatter(state, exception), values));
        }
    }

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
