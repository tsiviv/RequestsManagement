using Bogus;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Enums;
using RequestsManagement.Models;

namespace RequestsManagement.Data.Seeding;

public static class RequestSeeder
{
    private const int RecordCount = 12_000;
    private const int BatchSize = 1_000;

    // Fixed anchor instead of DateTime.UtcNow so seed output is fully
    // reproducible regardless of which day the seed actually runs.
    private static readonly DateTime AnchorDate = new(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] RequestTypes =
    [
        "Service outage", "Billing dispute", "Access request", "Data export",
        "Account verification", "Contract renewal", "Refund request", "Technical support",
        "Onboarding", "Compliance review", "Feature request", "Password reset",
        "Integration issue", "Invoice correction", "License upgrade", "Security audit",
        "Vendor inquiry", "Delivery delay", "Product defect", "Cancellation request"
    ];

    private static readonly string[] RequestSubjects =
    [
        "production environment", "customer portal", "monthly invoice", "API integration",
        "user account", "mobile application", "payment gateway", "support ticket queue",
        "third-party vendor", "internal dashboard", "email notifications", "data pipeline",
        "employee onboarding", "annual contract", "shipping order", "compliance report"
    ];

    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Requests.AnyAsync(cancellationToken))
        {
            return;
        }

        Randomizer.Seed = new Random(20260824);

        var statuses = Enum.GetValues<RequestStatus>();
        var priorities = Enum.GetValues<RequestPriority>();

        var faker = new Faker<RequestEntity>()
            .RuleFor(r => r.Title, f => $"{f.PickRandom(RequestTypes)} - {f.PickRandom(RequestSubjects)}")
            .RuleFor(r => r.OrganizationName, f => f.Company.CompanyName())
            .RuleFor(r => r.Status, f => f.PickRandom(statuses))
            .RuleFor(r => r.Priority, f => f.PickRandom(priorities))
            .RuleFor(r => r.CreatedAt, f => f.Date.Between(AnchorDate.AddMonths(-18), AnchorDate))
            .RuleFor(r => r.UpdatedAt, (f, r) => f.Date.Between(r.CreatedAt, AnchorDate));

        for (var offset = 0; offset < RecordCount; offset += BatchSize)
        {
            var batch = faker.Generate(BatchSize);
            await context.Requests.AddRangeAsync(batch, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }
    }
}
