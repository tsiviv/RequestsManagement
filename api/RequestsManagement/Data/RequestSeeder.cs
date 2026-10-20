using System.Diagnostics;
using Bogus;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Enums;
using RequestsManagement.Models;

namespace RequestsManagement.Data;

public static class RequestSeeder
{
    private const int RecordCount = 100_000;
    private const int BatchSize = 2_000;

    // Fixed anchor instead of DateTime.UtcNow so seed output is fully
    // reproducible regardless of which day the seed actually runs.
    private static readonly DateTime AnchorDate = new(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc);

    private static readonly string[] RequestTypes =
    [
        "תקלת שירות", "מחלוקת בחיוב", "בקשת גישה", "ייצוא נתונים",
        "אימות חשבון", "חידוש חוזה", "בקשת החזר כספי", "תמיכה טכנית",
        "קליטת עובד", "בדיקת ציות", "בקשת פיצ'ר", "איפוס סיסמה",
        "תקלת אינטגרציה", "תיקון חשבונית", "שדרוג רישיון", "ביקורת אבטחה",
        "פנייה מספק", "עיכוב במשלוח", "פגם במוצר", "בקשת ביטול"
    ];

    private static readonly string[] RequestSubjects =
    [
        "סביבת הפרודקשן", "פורטל הלקוחות", "החשבונית החודשית", "אינטגרציית ה-API",
        "חשבון המשתמש", "האפליקציה הניידת", "שער התשלומים", "תור פניות התמיכה",
        "ספק חיצוני", "לוח הבקרה הפנימי", "התראות דוא\"ל", "צינור הנתונים",
        "קליטת עובדים", "החוזה השנתי", "הזמנת המשלוח", "דוח הציות"
    ];

    private static readonly string[] OrganizationNames =
    [
        "חברת התוכנה הישראלית בע\"מ", "קבוצת פתרונות דיגיטליים", "מערכות מידע מתקדמות בע\"מ",
        "טכנולוגיות ענן ישראל", "רשת בתי החולים הכללית", "בנק הפועלים לישראל",
        "חברת החשמל לישראל", "משרד התחבורה", "עיריית תל אביב-יפו", "אוניברסיטת תל אביב",
        "קופת חולים כללית", "חברת נמל אשדוד", "התעשייה האווירית לישראל", "רכבת ישראל",
        "משרד הבריאות", "בזק החברה הישראלית לתקשורת", "שופרסל בע\"מ",
        "אל על נתיבי אוויר לישראל", "פרטנר תקשורת", "סלקום ישראל"
    ];

    // Combined with OrganizationNames to give ~200 distinct organizations, so filtering
    // by organization is selective enough to reflect real index behavior at 100K rows.
    private static readonly string[] Branches =
    [
        "מטה", "סניף צפון", "סניף דרום", "סניף מרכז", "סניף ירושלים",
        "סניף חיפה", "סניף באר שבע", "אגף כספים", "אגף משאבי אנוש", "אגף מערכות מידע"
    ];

    private static readonly string[] Assignees =
    [
        "דנה כהן", "יוסי לוי", "מיכל אברהם", "אבי מזרחי", "נועה פרידמן",
        "רון ביטון", "שירה גולן", "עומר שפירא", "תמר אזולאי", "איתי רוזן",
        "הילה דהן", "גיל אוחיון", "ליאת ברק", "אלון חדד", "מאיה נחום"
    ];

    // Skewed like a real queue: most requests end up Completed and few are High priority,
    // which also makes "open"/"high priority" filters selective rather than ~25% of the table.
    private static readonly RequestStatus[] Statuses =
        [RequestStatus.New, RequestStatus.InProgress, RequestStatus.Waiting, RequestStatus.Completed];
    private static readonly float[] StatusWeights = [0.15f, 0.20f, 0.10f, 0.55f];

    private static readonly RequestPriority[] Priorities =
        [RequestPriority.Low, RequestPriority.Medium, RequestPriority.High];
    private static readonly float[] PriorityWeights = [0.40f, 0.40f, 0.20f];

    /// <summary>
    /// Seeds the test data set when the table is empty. With <paramref name="reset"/>, existing
    /// requests (and, via cascade, their history) are deleted first so the data set is recreated.
    /// Runs in one transaction so a failure never leaves a partially seeded table behind.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, ILogger logger, bool reset = false, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        if (reset)
        {
            var deleted = await context.Requests.ExecuteDeleteAsync(cancellationToken);
            if (deleted > 0)
            {
                // Restart Ids at 1 so a recreated data set has the same Ids as a fresh one.
                // Only after a delete: on a never-used table RESEED 0 would make the next Id 0.
                await context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Requests', RESEED, 0);", cancellationToken);
            }
            logger.LogWarning("Reseed requested: deleted {Count} existing requests", deleted);
        }
        else if (await context.Requests.AnyAsync(cancellationToken))
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        Randomizer.Seed = new Random(20260824);

        var faker = new Faker<RequestEntity>()
            .RuleFor(r => r.Title, f => $"{f.PickRandom(RequestTypes)} - {f.PickRandom(RequestSubjects)}")
            .RuleFor(r => r.OrganizationName, f => $"{f.PickRandom(OrganizationNames)} - {f.PickRandom(Branches)}")
            .RuleFor(r => r.AssignedTo, f => f.PickRandom(Assignees))
            .RuleFor(r => r.Status, f => f.Random.WeightedRandom(Statuses, StatusWeights))
            .RuleFor(r => r.Priority, f => f.Random.WeightedRandom(Priorities, PriorityWeights))
            .RuleFor(r => r.CreatedAt, f => f.Date.Between(AnchorDate.AddMonths(-18), AnchorDate))
            .RuleFor(r => r.UpdatedAt, (f, r) => f.Date.Between(r.CreatedAt, AnchorDate));

        // Inserts are append-only, so change detection on every Add is wasted work.
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            for (var offset = 0; offset < RecordCount; offset += BatchSize)
            {
                var batch = faker.Generate(BatchSize);
                await context.Requests.AddRangeAsync(batch, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                context.ChangeTracker.Clear();
            }
        }
        finally
        {
            context.ChangeTracker.AutoDetectChangesEnabled = true;
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} requests in {ElapsedMs} ms", RecordCount, stopwatch.ElapsedMilliseconds);
    }
}
