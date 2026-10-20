# Requests Management

מערכת לניהול פניות מארגונים: REST API ב-ASP.NET Core מול SQL Server, וממשק Angular. משתמשים רבים יכולים במקביל לאתר פניות, לסנן, למיין, לעדכן סטטוס (בודד או Bulk), לצפות בהיסטוריית השינויים ובנתונים מסכמים, על 100,000 רשומות.

> **הערה לגבי הקוד הקיים:** חלק מהקוד היה קיים לפני תחילת המבדק. בעבר ניגשתי למשרה במשרד להגנת הסביבה, ובמבדק שם ניתן אותו בסיס פרויקט. השתמשתי בו כנקודת מוצא, והמבדק הנוכחי נבנה מעליו. ניתוח הפערים שהושלמו מופיע ב-[docs/WORK_PLAN.md](docs/WORK_PLAN.md).

**מסמכים:**
- [docs/WORK_PLAN.md](docs/WORK_PLAN.md): תכנון העבודה. פירוק לפיצ'רים ולמשימות, תלויות, סדר ביצוע, הערכות מאמץ, ומה השתנה בביצוע.
- [docs/PERFORMANCE.md](docs/PERFORMANCE.md): מדידת ביצועים על 100,000 רשומות. Execution Plans, אינדקסים, Bottlenecks ושיפורים.

## טכנולוגיות וגרסאות

| שכבה | טכנולוגיה |
|---|---|
| Backend | .NET 9, ASP.NET Core Web API, EF Core 9.0.9 |
| בסיס נתונים | SQL Server (ברירת מחדל: LocalDB) |
| Frontend | Angular 21.2, Angular Material 21.2, RxJS 7.8, TypeScript 5.9 |
| בדיקות | xUnit 2.9 + `WebApplicationFactory`, Vitest 4 |
| נתוני בדיקה / תיעוד API | Bogus 35.6 / Swagger (Swashbuckle 6.6) |

## הרצה

**דרישות מקדימות:**
- .NET 9 SDK.
- Node.js 20.19+ או 22.12+ (דרישה של Angular 21).
- SQL Server LocalDB (Windows). על מערכות אחרות, ראו "SQL Server אחר" בהמשך.

```bash
# 1. API: בעלייה הראשונה מריץ migrations ויוצר 100,000 רשומות (כחצי דקה עד דקה)
cd api/RequestsManagement
dotnet run --launch-profile http          # http://localhost:5088/swagger

# 2. ממשק, בטרמינל נוסף
cd ui
npm install
npm start                                 # http://localhost:4200

# 3. בדיקות
cd api && dotnet test                     # Unit + Integration (מול SQL Server)
cd ui && npx ng test --watch=false        # Angular
```

- **שם משתמש:** לפני עדכון סטטוס בממשק יש להזין שם בשדה שבראש העמוד. השם נרשם כ-`ChangedBy` בהיסטוריה.
- **Swagger:** הכותרת `X-User-Name` מופיעה עם ערך ברירת מחדל, ואפשר לנסות את העדכונים ישירות.
- **דוגמאות מוכנות:** [RequestsManagement.http](api/RequestsManagement/RequestsManagement.http).

**יצירה חוזרת של נתוני הבדיקה:**
- הפקודה: `dotnet run --launch-profile http -- --reseed`.
- מה היא עושה: מוחקת את כל הפניות ואת ההיסטוריה שלהן, ויוצרת מחדש את אותן 100,000 רשומות בדיוק (seed ותאריך קבועים).
- הכל בטרנזקציה אחת: אם הפעולה נכשלת באמצע, הנתונים הקודמים נשארים.

**SQL Server אחר (Mac / Linux / Docker):** כל SQL Server מתאים. מעבירים connection string דרך משתנה סביבה, בלי שינוי קוד:

```bash
# לדוגמה, SQL Server ב-Docker:
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Your_str0ng_Pass' -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest

export ConnectionStrings__DefaultConnection='Server=localhost,1433;Database=RequestsManagement;User Id=sa;Password=Your_str0ng_Pass;TrustServerCertificate=True'
export REQUESTS_IT_SQLSERVER='Server=localhost,1433;User Id=sa;Password=Your_str0ng_Pass;TrustServerCertificate=True'   # לבדיקות האינטגרציה
```

## מבנה הפתרון

```
api/RequestsManagement/                ה-API
  Controllers/   נקודות קצה דקות: מקבלות קלט ומחזירות את תוצאת ה-Service
  Services/      לוגיקה: שליפה, עדכון בודד ו-Bulk, היסטוריה, Cache, זיהוי המשתמש
  Policies/      כללי מעברי הסטטוס: מקור אמת יחיד
  DTOs/ Mapping/ חוזה ה-API ומיפוי יחיד Entity→DTO (Projection שמתורגם ל-SQL)
  Models/ Data/  ישויות, הגדרות EF ואינדקסים, Seeding, Migrations
  Middleware/    טיפול מרוכז בשגיאות (ProblemDetails)
  Extensions/    רישום DI, הודעות ולידציה בעברית, תיעוד הכותרת ב-Swagger
api/RequestsManagement.Tests/              בדיקות Unit
api/RequestsManagement.IntegrationTests/   בדיקות Integration מול SQL Server
ui/src/app/
  core/                interceptors (שגיאות HTTP, שם המשתמש), שם המשתמש
  features/requests/   ה-Service היחיד שפונה ל-API, הדף שמחזיק את המצב, וקומפוננטות תצוגה
```

**עקרונות:**
- **Backend:**
  - DI עם Lifetimes מתאימים: Service ו-DbContext ב-Scoped, ה-Cache ב-Singleton.
  - Async ו-`CancellationToken` עד בסיס הנתונים.
  - טיפול מרוכז בשגיאות ב-Middleware אחד. `500` מחזיר הודעה כללית, בלי Stack Trace.
- **Logging:**
  - נקודות משמעותיות: שינוי סטטוס, קונפליקט, סיכום Bulk, Seeding.
  - בלי מידע רגיש: שם המשתמש וערכי Header לא נרשמים, ותווי בקרה מוחלפים כדי למנוע זיוף שורות לוג.
- **Angular:**
  - `switchMap` מבטל בקשה קודמת כשהשאילתה משתנה, ו-debounce של 300 ms לחיפוש.
  - מצבי Loading, Empty ו-Error.
  - `409` מוסבר למשתמש, והעמוד נטען מחדש.

## בסיס הנתונים והאינדקסים

**למה SQL Server:**
- **`rowversion` מובנה:** בסיס הנתונים עצמו אוכף Optimistic Concurrency (`UPDATE ... WHERE RowVersion = @rv`).
- **טרנזקציה אחת:** לשינוי הסטטוס ולשורת ה-Audit שלו.
- **אינדקסים מורכבים ו-Execution Plans:** לסינון, למיון ולמדידה.
- **LocalDB:** הרצה בלי התקנת שרת.

**חלופות שנשקלו:**
- **MongoDB:** Concurrency דרך שדה גרסה ידני, וטרנזקציה בין מסמכים דורשת Replica Set. אין כאן יתרון למודל מסמכים, כי המבנה קבוע ורלציוני.
- **PostgreSQL:** מתאים באותה מידה, עם `xmin`. LocalDB הכריע.

**איך נמנעת טעינה של כל הנתונים לזיכרון:**
- **כל העבודה ב-SQL:** כל פילטר, מיון ודפדוף מצטרפים ל-`IQueryable` ומתורגמים ל-`WHERE` / `ORDER BY` / `OFFSET-FETCH` עם פרמטרים.
- **רק העמוד מגיע לזיכרון:** לכל היותר 100 שורות, ורק עמודות ה-DTO (Projection, `AsNoTracking`).
- **הספירה:** `COUNT` בבסיס הנתונים. בלי פילטרים, הסה"כ נלקח מה-Summary שב-Cache.
- **Aggregations:** `GROUP BY` בבסיס הנתונים, כך שחוזרות 7 שורות.

**האינדקסים:** העמודה המובילה היא זו שמסננים לפיה בשוויון, והשנייה היא סדר המיון. כך אותו אינדקס משמש ל-Seek, לטווח ולסדר, ובלי Sort.

| אינדקס | תרחיש |
|---|---|
| `(Status, CreatedAt)` | סינון לפי סטטוס, החדשים קודם. `GROUP BY Status` |
| `(Priority, CreatedAt)` | סינון ומיון לפי עדיפות. `GROUP BY Priority` |
| `(AssignedTo, CreatedAt)` | "התור של מטפל", מטפל + טווח תאריכים |
| `(OrganizationName, CreatedAt)` | סינון ומיון לפי ארגון |
| `(CreatedAt)` | רשימת ברירת המחדל, טווח תאריכים |
| `(UpdatedAt)` | מיון לפי עדכון אחרון |
| `RequestStatusHistory (RequestId, ChangedAt)` | היסטוריית פנייה, מהחדש לישן |

**ההשפעה:** תרחישי הסינון והמיון הנפוצים ירדו מסריקה של כל הטבלה (2,539 עמודים, עד 1.4 שניות) ל-17–386 עמודים ו-2–5 ms. פירוט ב-[docs/PERFORMANCE.md](docs/PERFORMANCE.md).

## API

| Endpoint | תיאור |
|---|---|
| `GET /api/requests` | רשימה מדופדפת, מסוננת וממוינת |
| `GET /api/requests/summary` | Aggregations: סה"כ, לפי סטטוס ולפי עדיפות |
| `GET /api/requests/filter-options` | הארגונים והמטפלים הקיימים, לרשימות הבחירה בסינון |
| `PATCH /api/requests/{id}/status` | עדכון סטטוס, עם `rowVersion` |
| `GET /api/requests/{id}/history` | היסטוריית שינויי הסטטוס, מהחדש לישן |
| `POST /api/requests/status/bulk` | עדכון סטטוס של עד 100 פניות |

**פרמטרים של `GET /api/requests`** (ניתנים לשילוב):

| פרמטר | התנהגות |
|---|---|
| `search` | חיפוש חלקי ב-`Title` או ב-`OrganizationName` |
| `status`, `priority`, `organizationName`, `assignedTo` | התאמה מדויקת |
| `createdFrom`, `createdTo` | טווח חצי-פתוח: `createdFrom <= CreatedAt < createdTo` |
| `sortBy` | `createdAt` (ברירת מחדל), `updatedAt`, `organizationName`, `priority`, `id` |
| `sortDirection` | `asc` / `desc` (ברירת מחדל) |
| `page`, `pageSize` | `pageSize` בין 1 ל-100 (ברירת מחדל 20) |

**סדר המיון:**
- **כל מיון מסתיים ב-`Id`,** כך שהדפדוף יציב.
- **ארגון ועדיפות** ממוינים בשני לפי תאריך יצירה, כדי שהסדר יתאים בדיוק לאינדקס.
- **בכוונה אין מיון לפי:**
  - סטטוס ומטפל: מעט ערכים, ויש להם סינון.
  - כותרת: הסדר שלה חסר משמעות למשתמש.

**שגיאות:** כל שגיאה מוחזרת כ-ProblemDetails (`application/problem+json`), עם הודעה בעברית. הודעות ה-Model Binding של ASP.NET מתורגמות גם הן.

| קוד | מתי |
|---|---|
| `400` | קלט לא תקין, סטטוס לא חוקי, מעבר סטטוס לא מותר, חסר שם משתמש |
| `404` | פנייה לא קיימת |
| `409` | התנגשות בעדכון (`rowVersion` מיושן) |

**כללי מעבר סטטוס:**
- `New` → `InProgress`.
- `InProgress` → `Waiting` / `Completed`.
- `Waiting` → `InProgress` / `Completed`.
- `Completed` הוא סטטוס סופי.

כל פנייה ברשימה כוללת `allowedNextStatuses`, כך שהממשק לא משכפל את הכללים.

## Concurrency ומניעת Lost Update

**המנגנון:** לכל פנייה יש `RowVersion` ש-SQL Server מעלה בכל עדכון. הלקוח מחזיר את הגרסה שקרא, והשרת בודק אותה בשתי שכבות:
1. **בדיקה מוקדמת:** גרסה מיושנת מקבלת `409`, עוד לפני בדיקת המעבר. לקוח שהנתון אצלו מיושן צריך "רענן", ולא `400`.
2. **בבסיס הנתונים:** `UPDATE ... WHERE Id = @id AND RowVersion = @rv`. מבין שני עדכונים מקבילים, רק אחד מתאים לשורה. השני מקבל `409`.

**עקביות:**
- **אטומיות:** שורת ה-Audit נשמרת באותה טרנזקציה, ומתבטלת יחד עם עדכון שנכשל.
- **חותם זמן אחד:** `UpdatedAt` ו-`ChangedAt` מקבלים את אותו ערך.
- **גרסה חדשה:** ה-`RowVersion` החדש חוזר בתשובה.

**בדיקה מול SQL Server:** 8 עדכונים מקבילים עם אותו `rowVersion` → בדיוק `200` אחד, ושבעה `409`. נרשמת שורת היסטוריה אחת.

**ההיסטוריה:** כל שינוי נשמר כשורת Audit עם `RequestId`, `PreviousStatus`, `NewStatus`, `ChangedAt`, `ChangedBy`.

## עדכון Bulk

```json
POST /api/requests/status/bulk        (כותרת X-User-Name)
{ "status": "Completed", "items": [ { "id": 17, "rowVersion": "AAAAAAAAB9E=" }, { "id": 42, "rowVersion": "AAAAAAAAC1A=" } ] }

200 OK
{ "requested": 2, "updated": 1, "failed": 1,
  "results": [ { "id": 17, "outcome": "Updated", "request": { "...": "כולל rowVersion חדש" } },
               { "id": 42, "outcome": "Conflict", "error": "פנייה 42 עודכנה על ידי משתמש אחר מאז שנטענה. יש לרענן ולנסות שוב." } ] }
```

**ההתנהגות: Partial Success.**
- **כל פנייה נשמרת בטרנזקציה משלה,** עם ה-`rowVersion` שלה ושורת ה-Audit שלה.
- **לכל פנייה מוחזרת תוצאה, בסדר הקלט:**
  - `Updated`: הפנייה עודכנה.
  - `NotFound`: אין פנייה עם המזהה הזה.
  - `Conflict`: משתמש אחר עדכן אותה בינתיים.
  - `InvalidTransition`: המעבר לא מותר.
- **הספירות:** `updated` ו-`failed` מסכמים כמה הצליחו וכמה נכשלו.
- **`400` בלי לעדכן דבר:** שגיאה ברמת הבקשה כולה, כמו 0 או יותר מ-100 פריטים, מזהה כפול או סטטוס חסר.

**הנימוק:**
- **למה לא All-or-Nothing:** במערכת מרובת משתמשים צפוי שחלק מהפניות עודכנו בינתיים, ו-All-or-Nothing היה מכשיל עד 99 עדכונים תקינים בגלל קונפליקט אחד.
- **על מה ויתרנו:** אטומיות בין פניות שונות, שאין לה ערך עסקי כאן.
- **למה לא `207 Multi-Status`:** הוא בא מ-WebDAV, ולקוחות HTTP לא מטפלים בו. `200` עם ספירות מפורשות ברור יותר.

## Cache

**מה נשמר:** ה-Summary (סה"כ, לפי סטטוס ולפי עדיפות).
- **השאילתה היקרה ביותר:** `COUNT` ושני `GROUP BY` על כל הטבלה, כ-230 ms, בכל טעינת מסך.
- **התוצאה זהה לכל המשתמשים.**
- **רשימות מסוננות לא נשמרות:** מרחב המפתחות גדול מדי, וה-Invalidation שלהן מורכב.

**Expiration:** `AbsoluteExpiration` של 60 שניות. זו רשת ביטחון לשינויים שלא עוברים דרך הקוד.

**Invalidation:**
- **מתי:** אחרי כל שינוי סטטוס שהצליח, בודד או פריט ב-Bulk, ואחרי Seeding.
- **אחרי ה-commit, לא לפניו:** אחרת קורא מקביל היה מחשב מחדש מהנתונים הישנים.
- **עדכון שנכשל לא מבטל:** שום נתון לא השתנה.
- **המפתח כולל מספר גרסה:** קורא שחישב לפני ה-commit לא יכול לשמור תוצאה ישנה אחרי ה-Invalidation.

**כמה מופעי שרת:**
- **הבעיה:** `IMemoryCache` שייך לתהליך אחד. מאחורי Load Balancer, שרת אחר היה מגיש נתון ישן עד 60 שניות.
- **ההתאמה:** החלפת המימוש של `IRequestSummaryCache` במימוש מעל Redis (`IDistributedCache`), כשמספר הגרסה מנוהל ב-Redis (`INCR`). שאר הקוד לא משתנה.

## בדיקות

| פרויקט | סוג | מה מכוסה |
|---|---|---|
| `RequestsManagement.Tests` | Unit, מול EF InMemory | שליפה, סינון ומיון, כל 16 צירופי המעברים, Audit, Bulk, Cache ו-Invalidation, ולידציה |
| `RequestsManagement.IntegrationTests` | Integration: API מלא (`WebApplicationFactory`) מול **SQL Server אמיתי** | Concurrency אמיתי, ביטול ה-Audit בקונפליקט, ולידציה דרך ה-pipeline (כולל עברית), Bulk, היסטוריה, Cache |
| `ui` | Vitest | Debounce, ביטול בקשות, מצבי UI, 409/400, Bulk, היסטוריה, Aggregations |

**תשתית האינטגרציה:**
- **מסד ייחודי לכל הרצה:** נוצר מה-migrations ונמחק בסוף.
- **בלי Seeding:** האפליקציה עולה עם `Seeding:Enabled=false`, וכל בדיקה יוצרת רק את השורות שהיא צריכה.

## החלטות טכנולוגיות וחלופות

**1. Optimistic Concurrency, ולא Pessimistic Locking או Last-Write-Wins.**
- **Last-Write-Wins:** דורס בשקט שינוי של משתמש אחר. זה בדיוק ה-Lost Update שהאפיון אוסר.
- **Pessimistic Locking:** לא מתאים ל-REST API חסר מצב. לעדכון קצר אין נקודת נעילה ושחרור טבעית, ונעילה שלא שוחררה חוסמת משתמשים אחרים.
- **Optimistic:** בלי מצב בשרת, בדיקה אחת בתוך ה-`UPDATE`. התנגשויות נדירות, ולכן `409` ברור הוא הפשרה הנכונה.

**2. Bulk עם Partial Success, ולא All-or-Nothing:** ראו הנימוק בסעיף "עדכון Bulk".

## מגבלות ידועות

- **זהות המשתמש מבוססת Header, בלי Authentication:** `X-User-Name` לא מאומת. ההחלפה ל-JWT היא במקום אחד (`ICurrentUser`).
- **חיפוש חופשי:** `LIKE '%x%'` לא משתמש באינדקס, וה-`COUNT` שלו סורק את כל הטבלה, כ-0.5–1 שנייה ב-100,000 רשומות.
- **Bulk שנקטע באמצע,** למשל כשבסיס הנתונים נופל: הפריטים שנשמרו נשארים שמורים. כל אחד מהם עקבי, והלקוח צריך לטעון מחדש.

## מה הייתי משפר

- **Authentication ו-Authorization** לסביבה אמיתית.
- **חיפוש מהיר יותר:**
  - collation בינארי בתנאי החיפוש. נמדד: פי 4.5, עם אותן תוצאות על הנתונים בעברית.
  - Full-Text Search בהיקף גדול יותר.

## שימוש ב-AI

נעשה שימוש ב-Claude Code לאורך המבדק:
- מימוש רכיבים ב-Backend וב-Angular.
- כתיבת בדיקות, איתור תקלות ו-Code Review.
- בחינת חלופות ושיקולי ביצועים.

הבחירה בפתרון, ההבנה של הקוד והאחריות עליו הן שלי. אחרי כל שינוי משמעותי עברתי על הקוד, ובדקתי אותו בבדיקות אוטומטיות ובהרצה בפועל.

**החלטות שקיבלתי בעצמי:**
- **שדות המיון:** בקוד המקורי היה מיון לפי סטטוס ועדיפות. הסרתי מיון לפי סטטוס, ובהמשך גם לפי מטפל, כי לשדות עם מעט ערכים יש סינון. השארתי עדיפות, שיש לה סדר עסקי, וארגון.
- **שפת ההודעות:** דרשתי שכל הודעות השגיאה של השרת יהיו בעברית.
- **תוכנית העבודה:**
  - קבעתי את הערכות הזמן.
  - הקדמתי את הבדיקות הסופיות לפני התיעוד.
  - אישרתי כל משימה לפני המעבר לבאה.

**מה נבדק ולא התקבל כמו שהוא:**
- **כל טענת ביצועים נמדדה מול SQL Server אמיתי** (`STATISTICS IO`, Plans). כך התגלה, למשל, שעמוד עמוק קורא 153,454 עמודים, וזה תוקן.
- **בבדיקות המרכזיות וידאתי שהבדיקה נכשלת כשמכניסים את הבאג בכוונה.** למשל, הסרת ההגנה מ-Lost Update, או `mergeMap` במקום `switchMap`.
- **בדיקות האינטגרציה ובדיקה בדפדפן אמיתי חשפו באגים שהבדיקות האחרות לא תפסו:**
  - Content-Type שגוי בשגיאות.
  - תווית נגישות שלא הגיעה לדף.
  - פורמט תאריך.
