using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RequestsManagement.Data;
using RequestsManagement.Enums;
using RequestsManagement.Models;
using RequestsManagement.Services;

namespace RequestsManagement.IntegrationTests;

/// <summary>
/// Hosts the real API in memory (WebApplicationFactory) against a real SQL Server database, so the
/// tests exercise what InMemory cannot: SQL translation, rowversion generation, transactions and
/// the database-enforced concurrency check.
/// </summary>
/// <remarks>
/// One uniquely named database per test run, created by the app's own migrations at startup and
/// dropped at the end. Defaults to LocalDB; set REQUESTS_IT_SQLSERVER to a connection string
/// (without a database name) to use another SQL Server, e.g. in CI. Startup seeding is disabled:
/// each test starts from an empty database (<see cref="ResetAsync"/>) and inserts only what it needs.
/// </remarks>
public sealed class SqlServerApiFixture : IAsyncLifetime
{
    public const string DefaultUser = "it-user";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program> _factory = null!;

    public string ConnectionString { get; private set; } = "";

    public Task InitializeAsync()
    {
        var server = new SqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("REQUESTS_IT_SQLSERVER")
            ?? @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True")
        {
            InitialCatalog = $"RequestsManagement_IT_{Guid.NewGuid():N}"
        };
        ConnectionString = server.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
            builder.UseSetting("Seeding:Enabled", "false");
        });

        // Starting the server runs Program's startup: migrations create the database.
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await using (var context = CreateDbContext())
        {
            await context.Database.EnsureDeletedAsync();
        }
        await _factory.DisposeAsync();
    }

    /// <summary>A client acting as <paramref name="userName"/> (percent-encoded, as the Angular client sends it).</summary>
    public HttpClient CreateClient(string? userName = DefaultUser)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (userName is not null)
        {
            client.DefaultRequestHeaders.Add(HeaderCurrentUser.HeaderName, Uri.EscapeDataString(userName));
        }
        return client;
    }

    /// <summary>A context on the test database, outside the app (for arranging and verifying).</summary>
    public AppDbContext CreateDbContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(interceptors)
            .Options);

    /// <summary>The app's own singleton cache, so tests that write behind the API can invalidate it.</summary>
    public IRequestSummaryCache SummaryCache => _factory.Services.GetRequiredService<IRequestSummaryCache>();

    /// <summary>Empty tables and an empty summary cache: every test starts from the same state.</summary>
    public async Task ResetAsync()
    {
        await using var context = CreateDbContext();
        await context.Requests.ExecuteDeleteAsync(); // history goes with it (ON DELETE CASCADE)
        SummaryCache.Invalidate();
    }

    /// <summary>Inserts requests directly; SQL Server assigns Id and RowVersion.</summary>
    public async Task<List<RequestEntity>> SeedAsync(params RequestEntity[] requests)
    {
        await using var context = CreateDbContext();
        context.Requests.AddRange(requests);
        await context.SaveChangesAsync();
        SummaryCache.Invalidate();
        return requests.ToList();
    }

    public static RequestEntity Request(
        string title,
        RequestStatus status = RequestStatus.New,
        string organization = "Org",
        string assignedTo = "Handler",
        DateTime? createdAt = null,
        RequestPriority priority = RequestPriority.Low)
    {
        var created = createdAt ?? new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        return new RequestEntity
        {
            Title = title,
            OrganizationName = organization,
            AssignedTo = assignedTo,
            Status = status,
            Priority = priority,
            CreatedAt = created,
            UpdatedAt = created
        };
    }
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerApiFixture>
{
    // All integration tests share one database and run sequentially (one xUnit collection),
    // each resetting it first, so they never see each other's rows.
    public const string Name = "SQL Server";
}
