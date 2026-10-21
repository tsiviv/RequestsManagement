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

// Real SQL Server, for what InMemory cannot test: SQL translation, rowversion and transactions.
// One database per run, created by the migrations and dropped at the end. Defaults to LocalDB;
// REQUESTS_IT_SQLSERVER (a connection string without a database) selects another server.
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

    public AppDbContext CreateDbContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(interceptors)
            .Options);

    public IRequestSummaryCache SummaryCache => _factory.Services.GetRequiredService<IRequestSummaryCache>();

    public async Task ResetAsync()
    {
        await using var context = CreateDbContext();
        await context.Requests.ExecuteDeleteAsync(); // history goes with it (ON DELETE CASCADE)
        SummaryCache.Invalidate();
    }

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
