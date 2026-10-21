# Requests Management

מערכת לניהול פניות מארגונים: REST API ב-ASP.NET Core מול SQL Server, וממשק Angular. משתמשים רבים יכולים במקביל לאתר פניות, לסנן, למיין, לעדכן סטטוס (בודד או Bulk), לצפות בהיסטוריית השינויים ובנתונים מסכמים, על 100,000 רשומות.

> **הערה לגבי הקוד הקיים:** חלק מהקוד היה קיים לפני תחילת המבדק. בעבר ניגשתי למשרה במשרד להגנת הסביבה, ובמבדק שם ניתן אותו בסיס פרויקט. השתמשתי בו כנקודת מוצא, והמבדק הנוכחי נבנה מעליו. ניתוח הפערים שהושלמו מופיע ב-[docs/WORK_PLAN.md](docs/WORK_PLAN.md).

**מסמכים:**
- [docs/WORK_PLAN.md](docs/WORK_PLAN.md): תכנון העבודה. פירוק לפיצ'רים ולמשימות, תלויות, סדר ביצוע, והערכות מאמץ.
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
- SQL Server LocalDB.

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

**שם משתמש:** לפני עדכון סטטוס בממשק יש להזין שם בשדה שבראש העמוד. השם נרשם כ-`ChangedBy` בהיסטוריה.

**יצירה חוזרת של נתוני הבדיקה:** `dotnet run --launch-profile http -- --reseed` מוחק את כל הפניות וההיסטוריה, ויוצר מחדש את אותן 100,000 רשומות בדיוק, בטרנזקציה אחת.

## מבנה הפתרון

```
api/RequestsManagement/                ה-API
  Controllers/   נקודות קצה דקות: מקבלות קלט ומחזירות את תוצאת ה-Service
  Services/      שליפה (RequestQueryService + RequestFilters / RequestSorting), עדכון סטטוס בודד ו-Bulk
                 (RequestStatusService + StatusUpdateValidation), Cache, זיהוי המשתמש
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

## בסיס הנתונים והאינדקסים

**למה SQL Server:** `rowversion` מובנה, כך שבסיס הנתונים עצמו אוכף את ה-Optimistic Concurrency; טרנזקציה אחת לשינוי הסטטוס ולשורת ה-Audit שלו; ואינדקסים מורכבים ו-Execution Plans למדידה. החלופה, MongoDB, הייתה דורשת שדה גרסה ידני ו-Replica Set לטרנזקציות, בלי יתרון למבנה הנתונים הקבוע והרלציוני.

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

המדידות וה-Execution Plans: [docs/PERFORMANCE.md](docs/PERFORMANCE.md).

## עדכון סטטוס ו-Concurrency

**כללי מעבר סטטוס:** `New` → `InProgress`; `InProgress` → `Waiting` / `Completed`; `Waiting` → `InProgress` / `Completed`; `Completed` סופי. מעבר אחר מחזיר `400`.

**מניעת Lost Update:** לכל פנייה יש `RowVersion` ש-SQL Server מעלה בכל עדכון. הלקוח מחזיר את הגרסה שקרא, והשרת בודק אותה בשתי שכבות:
1. **בדיקה מוקדמת:** גרסה מיושנת מקבלת `409`, עוד לפני בדיקת המעבר. לקוח שהנתון אצלו מיושן צריך "רענן", ולא `400`.
2. **בבסיס הנתונים:** `UPDATE ... WHERE Id = @id AND RowVersion = @rv`. מבין שני עדכונים מקבילים, רק אחד מתאים לשורה. השני מקבל `409`.

**עקביות:**
- **אטומיות:** שורת ה-Audit נשמרת באותה טרנזקציה, ומתבטלת יחד עם עדכון שנכשל.
- **חותם זמן אחד:** `UpdatedAt` ו-`ChangedAt` מקבלים את אותו ערך.
- **גרסה חדשה:** ה-`RowVersion` החדש חוזר בתשובה.

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

## החלטות טכנולוגיות וחלופות

**1. Optimistic Concurrency, ולא Pessimistic Locking או Last-Write-Wins.**
- **Last-Write-Wins:** דורס בשקט שינוי של משתמש אחר. זה בדיוק ה-Lost Update שהאפיון אוסר.
- **Pessimistic Locking:** לא מתאים ל-REST API חסר מצב. לעדכון קצר אין נקודת נעילה ושחרור טבעית, ונעילה שלא שוחררה חוסמת משתמשים אחרים.
- **Optimistic:** בלי מצב בשרת, בדיקה אחת בתוך ה-`UPDATE`. התנגשויות נדירות, ולכן `409` ברור הוא הפשרה הנכונה.

**2. Bulk עם Partial Success, ולא All-or-Nothing.**
- **All-or-Nothing:** במערכת מרובת משתמשים צפוי שחלק מהפניות עודכנו בינתיים, וקונפליקט אחד היה מכשיל עד 99 עדכונים תקינים.
- **על מה ויתרנו:** אטומיות בין פניות שונות, שאין לה ערך עסקי כאן. כל פנייה בפני עצמה עדיין אטומית.

## מגבלה ידועה

**חיפוש חופשי:** `LIKE '%x%'` לא משתמש באינדקס, וה-`COUNT` שלו סורק את כל הטבלה, כ-0.5–1 שנייה ב-100,000 רשומות.

## מה הייתי משפר

**חיפוש מהיר יותר:** collation בינארי בתנאי החיפוש (נמדד: פי 4.5, עם אותן תוצאות על הנתונים בעברית), ו-Full-Text Search בהיקף גדול יותר.

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
