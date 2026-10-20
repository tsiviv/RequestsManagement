using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RequestsManagement.Data;
using RequestsManagement.Extensions;
using RequestsManagement.Middleware;
using RequestsManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .AddHebrewValidationMessages();

builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.OperationFilter<UserNameHeaderOperationFilter>());

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
        // EF logs a client-cancelled connection open as an Error. Real connection failures still
        // surface as a 500 that ExceptionHandlingMiddleware logs at Error with the exception, so
        // demoting EF's own entry removes false alarms without hiding outages.
        .ConfigureWarnings(w => w.Log((RelationalEventId.ConnectionError, LogLevel.Information))));

builder.Services.AddApplicationServices();

const string AngularDevCorsPolicy = "AngularDev";
builder.Services.AddCors(options =>
    options.AddPolicy(AngularDevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    // `dotnet run -- --reseed` recreates the test data set from scratch. Integration tests turn
    // seeding off (Seeding:Enabled=false) and create exactly the rows each test needs.
    if (app.Configuration.GetValue("Seeding:Enabled", true))
    {
        await RequestSeeder.SeedAsync(db, app.Logger, reset: args.Contains("--reseed"));
    }
    // A no-op for the in-process cache (empty at startup), but required once the cache is shared
    // between instances (Redis): other servers would otherwise keep serving pre-reseed numbers.
    scope.ServiceProvider.GetRequiredService<IRequestSummaryCache>().Invalidate();
}

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(AngularDevCorsPolicy);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Lets the integration tests host the API in memory with WebApplicationFactory<Program>.
public partial class Program;
