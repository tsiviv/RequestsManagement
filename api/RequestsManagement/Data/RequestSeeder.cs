using Bogus;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.Enums;
using RequestsManagement.Models;

namespace RequestsManagement.Data;

public static class RequestSeeder
{
    private const int RecordCount = 12_000;
    private const int BatchSize = 1_000;

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
            .RuleFor(r => r.OrganizationName, f => f.PickRandom(OrganizationNames))
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
