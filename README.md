# Requests Management

אפליקציית ניהול בקשות ארגוניות: API מסוג ASP.NET Core REST עם ממשק Angular, מול בסיס נתונים SQL Server. המערכת תומכת בחיפוש, סינון, מיון ודפדוף בין בקשות, הצגת סיכום נתונים, ועדכון סטטוס של בקשה בצורה בטוחה גם כשכמה אנשים עשויים לערוך אותה בו-זמנית.

> **הערה לגבי הקוד הקיים:** חלק מהקוד היה קיים עוד לפני תחילת המבדק הנוכחי. בעבר ניגשתי למשרה במשרד להגנת הסביבה, ובמבדק שם ניתן אותו בסיס פרויקט. השתמשתי בבסיס הזה כנקודת מוצא, והמבדק הנוכחי נבנה מעליו (פירוט הפערים שהושלמו מופיע ב-[docs/WORK_PLAN.md](docs/WORK_PLAN.md)).

## טכנולוגיות

- Backend: .NET 9, ASP.NET Core, EF Core
- Frontend: Angular 21
- בסיס נתונים: SQL Server LocalDB
- בדיקות: xUnit (backend), Vitest (frontend)

## דרישות מקדימות

- .NET 9 SDK
- SQL Server LocalDB
- Node.js ו-npm

## איך מריצים

### Backend

```bash
cd api/RequestsManagement
dotnet restore
dotnet run --launch-profile http
```

- API: `http://localhost:5088`
- Swagger: `http://localhost:5088/swagger`

ה-migrations וה-seeding מתבצעים אוטומטית עם עליית האפליקציה — אין צורך בשלב ידני נוסף.

### Frontend

מתוך `ui/`:

```bash
npm install
npm start
```

- אפליקציה: `http://localhost:4200`
- ממשק המשתמש כולו בעברית, בפריסת RTL.

## Database ו-Seeding

- משתמש ב-SQL Server LocalDB.
- עם עליית האפליקציה, היא מריצה את כל ה-migrations הממתינים של EF Core, ולאחר מכן מבצעת seeding אם בסיס הנתונים ריק.
- ה-seeding יוצר 100,000 רשומות דטרמיניסטיות — נעשה שימוש ב-seed קבוע ובתאריך קבוע במקום ערכים אקראיים, כך שהנתונים זהים בכל מכונה ובכל הרצה (כ-10–20 שניות בעלייה הראשונה).
- ההתפלגות מכוונת להיות מציאותית ולא אחידה: כ-200 ארגונים, 15 מטפלים, רוב הפניות בסטטוס Completed ומיעוטן בעדיפות High — כך שסינונים נפוצים סלקטיביים כמו במערכת אמיתית.
- ה-seeding בודק תחילה אם כבר קיימים נתונים, כך שהרצה חוזרת של האפליקציה לעולם לא יוצרת כפילויות.

### יצירה חוזרת של נתוני הבדיקה

```bash
cd api/RequestsManagement
dotnet run --launch-profile http -- --reseed
```

הפקודה מוחקת את כל הפניות (וההיסטוריה שלהן, באמצעות cascade), מאפסת את המזהים ל-1 ויוצרת מחדש את אותן 100,000 רשומות בדיוק. כל התהליך רץ בטרנזקציה אחת — אם הוא נכשל באמצע, הנתונים הקודמים נשארים. לאחר מכן האפליקציה ממשיכה לעלות כרגיל.

## מבנה הפרויקט

Backend (`api/RequestsManagement/`):

- `Controllers/` — נקודות הקצה של ה-HTTP
- `Services/` — לוגיקה עסקית: סינון, מיון, אגרגציה, עדכוני סטטוס
- `DTOs/` — מבני הבקשה/תגובה
- `Mapping/` — מיפוי יחיד Entity→DTO (Expression שמתורגם ל-SQL, וגרסה מקומפלת לאובייקטים בזיכרון)
- `Models/` — ישויות EF Core
- `Data/` — DbContext, הגדרת הישויות, seeding
- `Enums/` — ערכי סטטוס ועדיפות
- `Exceptions/` / `Middleware/` — טיפול מרוכז בשגיאות

Frontend (`ui/src/app/`):

- `core/interceptors/` — טיפול מרוכז בשגיאות HTTP
- `shared/models/` — טיפוסים משותפים לכל הפיצ'רים
- `features/requests/` — פיצ'ר הבקשות: קומפוננטות, מודלים ושירות

## API מרכזיים

- `GET /api/requests` — רשימת בקשות מדופדפת, מסוננת וממוינת
- `GET /api/requests/summary` — סה"כ ספירות לפי סטטוס ועדיפות
- `PATCH /api/requests/{id}/status` — עדכון סטטוס של בקשה
- `GET /api/requests/{id}/history` — היסטוריית שינויי הסטטוס של בקשה, מהחדש לישן
- `POST /api/requests/status/bulk` — עדכון סטטוס של עד 100 בקשות בקריאה אחת (פירוט בהמשך)

נקודת הקצה של הרשימה תומכת ב:

| פרמטר | התנהגות |
|---|---|
| `search` | חיפוש חלקי ב-`Title` או ב-`OrganizationName` (עד 200 תווים) |
| `status`, `priority` | התאמה מדויקת לערך enum |
| `organizationName`, `assignedTo` | התאמה מדויקת (לאחר Trim) — כדי לאפשר Index Seek |
| `createdFrom`, `createdTo` | טווח חצי-פתוח: `createdFrom <= CreatedAt < createdTo`. כדי לכלול יום אחרון במלואו, שולחים את היום שאחריו |
| `sortBy`, `sortDirection` | `status` / `priority` / `createdAt` / `updatedAt`, `asc` / `desc` |
| `page`, `pageSize` | `pageSize` בין 1 ל-100 (ברירת מחדל 20) |

כל הפרמטרים ניתנים לשילוב. קלט לא תקין (שדה מיון לא מורשה, `createdFrom >= createdTo`, מחרוזת ארוכה מדי, תאריך לא תקין) מחזיר `400` בפורמט ProblemDetails. דוגמאות מוכנות להרצה: [RequestsManagement.http](api/RequestsManagement/RequestsManagement.http).

### עדכון סטטוס Bulk – חוזה

```http
POST /api/requests/status/bulk
X-User-Name: <שם המשתמש, percent-encoded UTF-8>
Content-Type: application/json

{
  "status": "Completed",
  "items": [
    { "id": 17, "rowVersion": "AAAAAAAAB9E=" },
    { "id": 42, "rowVersion": "AAAAAAAAC1A=" }
  ]
}
```

כל פריט נושא `rowVersion` משלו, כך שכל בקשה נבדקת ל-Concurrency בנפרד, בדיוק כמו בעדכון בודד.

**הבחירה: Partial Success.** כל בקשה מעודכנת באופן עצמאי, והתשובה מפרטת מה קרה לכל אחת:

```json
{
  "requested": 2,
  "updated": 1,
  "failed": 1,
  "results": [
    { "id": 17, "outcome": "Updated", "error": null, "request": { "id": 17, "status": "Completed", "rowVersion": "AAAAAAAAB+k=", "...": "..." } },
    { "id": 42, "outcome": "Conflict", "error": "Request 42 was modified by another user since it was loaded. Reload and try again.", "request": null }
  ]
}
```

| `outcome` | מתי | מה הלקוח עושה |
|---|---|---|
| `Updated` | הבקשה עודכנה ונרשמה לה שורת היסטוריה | מחליף את השורה ב-`request` (שכולל את ה-`rowVersion` החדש) |
| `NotFound` | אין בקשה עם המזהה הזה | מסיר אותה מהבחירה |
| `Conflict` | ה-`rowVersion` שנשלח מיושן: משתמש אחר עדכן את הבקשה בינתיים | טוען מחדש ומחליט שוב |
| `InvalidTransition` | המעבר אינו מותר לפי כללי המעברים, או שהבקשה כבר בסטטוס הזה | מוצגת ההודעה מ-`error` |

**קודי HTTP:**
- **`200`:** כל בקשה תקינה מבחינת מבנה, גם כשחלק מהפריטים, או כולם, נכשלו. `updated` / `failed` מסכמים, ו-`results` מופיעים בסדר הקלט.
- **`400`:** קלט שגוי ברמת הבקשה כולה, שמעיד על באג בלקוח ולא על מצב של פנייה מסוימת. אף פריט לא מעודכן. המקרים:
  - `status` חסר או לא חוקי.
  - `items` ריק או מכיל יותר מ-100 פריטים.
  - מזהה כפול.
  - `id` שאינו חיובי.
  - `rowVersion` חסר או לא בפורמט base64.
  - כותרת `X-User-Name` חסרה.

**למה Partial Success ולא All-or-Nothing:**
- **כל פנייה עצמאית:** לכל פנייה `rowVersion` ומחזור חיים משלה, ובמערכת מרובת משתמשים צפוי שחלק מ-100 פניות שנבחרו עודכנו בינתיים על ידי אחרים.
- **מה All-or-Nothing היה גורם:** קונפליקט אחד היה מכשיל 99 עדכונים תקינים, והמשתמש היה נאלץ לטעון מחדש ולנסות שוב, אולי שוב ושוב כשיש עומס.
- **מה אובד:** אטומיות בין פניות שונות, שאין לה ערך עסקי כאן. כל פנייה בפני עצמה עדיין מעודכנת אטומית: הסטטוס ושורת ההיסטוריה שלה נשמרים באותה טרנזקציה.
- **למה לא `207 Multi-Status`:** הוא בא מ-WebDAV, ורוב לקוחות ה-HTTP, כולל `HttpClient` של Angular, לא מטפלים בו באופן מיוחד. `200` עם `updated` / `failed` מפורשים ברור יותר.

**מימוש** (`RequestService.BulkUpdateStatusAsync`):
1. **ולידציה ברמת הבקשה כולה,** לפני שנוגעים בבסיס הנתונים: משתמש, סטטוס, כמות פריטים, מזהים כפולים ופורמט `rowVersion`.
2. **שאילתה אחת טוענת את כל הפניות** (`WHERE Id IN (...)`), במקום שאילתה לכל פריט.
3. **לכל פריט, לפי הסדר:**
   - לא קיים → `NotFound`.
   - `rowVersion` מיושן → `Conflict`.
   - מעבר לא מותר → `InvalidTransition`.
   - אחרת, `SaveChanges` נפרד לפריט: טרנזקציה משלו, עם הסטטוס ושורת ההיסטוריה יחד ו-`UPDATE ... WHERE RowVersion = @rv`.
4. **אותה לוגיקה כמו בעדכון בודד:** בדיקת המעברים (`StatusTransitionPolicy`), בדיקת ה-`rowVersion` וכתיבת ההיסטוריה משותפות לשני המסלולים.
5. **קונפליקט שמתגלה רק ב-`SaveChanges`** (כותב מתחרה בין הטעינה לשמירה) מדווח כ-`Conflict`. הישויות של אותו פריט מנותקות מה-`DbContext`, כדי שה-`SaveChanges` של הפריט הבא לא ינסה לשמור אותן שוב.

המחיר הוא עד 100 פניות לבסיס הנתונים בקריאה אחת. זה סביר לפעולה שמוגבלת ל-100 פריטים, וזה מה שמאפשר הצלחה חלקית. `SaveChanges` אחד לכל הפריטים היה מבטל את כולם בקונפליקט אחד.

**מגבלה:** אם השרת נכשל באמצע הריצה, למשל כשבסיס הנתונים נופל או כשהלקוח מבטל, פריטים שכבר נשמרו נשארים שמורים, והלקוח לא מקבל את התשובה המפורטת. כל פריט שנשמר עדיין עקבי, כולל שורת ההיסטוריה שלו. במקרה כזה הלקוח צריך לטעון מחדש. אפשר לשפר זאת עם Idempotency Key, כך שאותה פעולה תוכל להישלח שוב בבטחה.

## האתגר שנבחר – Concurrency

עדכוני סטטוס משתמשים ב-optimistic concurrency control.

לכל בקשה יש `RowVersion` שמשתנה אוטומטית בכל עדכון של השורה. כשמעדכנים סטטוס, הלקוח שולח בחזרה את ה-`rowVersion` האחרון שהוא קרא. אם משתמש אחר עדכן את אותה בקשה קודם, הגרסה כבר לא תואמת, והעדכון נדחה עם `409 Conflict` במקום לדרוס בשקט את השינוי של המשתמש האחר.

## החלטה טכנולוגית משמעותית

למה optimistic concurrency ולא pessimistic locking או last-write-wins:

- **Last-write-wins** היה מאפשר לשינוי של משתמש אחד לדרוס בשקט שינוי של משתמש אחר, בלי שום התראה לאף אחד מהצדדים.
- **Pessimistic locking** לא מתאים היטב ל-REST API חסר מצב (stateless) — עדכון סטטוס בודד וקצר אין לו נקודה טבעית לנעילה או שחרור נעילה.
- **Optimistic concurrency** מתאים כאן: אין session או נעילה לנהל בצד השרת, הבדיקה היא השוואת גרסה בודדת על גבי העדכון הקיים, וההתנגשויות נדירות מספיק כך שכישלון מהיר עם שגיאה ברורה הוא הפשרה הנכונה.

## שליפת נתונים: מניעת טעינה לזיכרון ואינדקסים

### איך נמנעת טעינת כלל הנתונים לזיכרון

`RequestService.GetRequestsAsync` בונה `IQueryable` ומבצע את כל העבודה בבסיס הנתונים. לזיכרון מגיעות רק השורות של העמוד המבוקש:

1. **`db.Requests.AsNoTracking()`** — שום דבר עוד לא נשלף. בנוסף, אין מעקב שינויים, כי זו שליפה לקריאה בלבד.
2. **כל פילטר מוסיף `Where`** לאותו `IQueryable`, ו-EF מתרגם אותו ל-`WHERE` עם פרמטרים. הפרמטרים מאפשרים שימוש חוזר ב-Execution Plan ומונעים SQL Injection.
3. **`CountAsync`** — `SELECT COUNT(*)` עם אותו `WHERE`. חוזר מספר אחד בלבד.
4. **עמוד מעבר לסוף התוצאות** — אם ה-offset גדול או שווה לסך התוצאות, השאילתה השנייה לא נשלחת בכלל.
5. **`ApplySort` + `Skip/Take`** מתורגמים ל-`ORDER BY ... , Id` ול-`OFFSET/FETCH`.
6. **`Select(RequestMappings.ToDtoProjection)`** — נשלפות רק העמודות של ה-DTO, ולא ישויות מלאות.
7. **`ToListAsync`** — זו נקודת ה-materialization היחידה, ומגיעות בה לכל היותר `pageSize` שורות (100 לכל היותר).

ה-SQL שנוצר בפועל (סינון לפי מטפל וטווח תאריכים, מיון לפי סטטוס):

```sql
SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[AssignedTo], [r].[Status],
       [r].[Priority], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[AssignedTo] = @__assignedTo_0
  AND [r].[CreatedAt] >= @__query_CreatedFrom_Value_1 AND [r].[CreatedAt] < @__query_CreatedTo_Value_2
ORDER BY [r].[Status] DESC, [r].[Id] DESC
OFFSET @__p_3 ROWS FETCH NEXT @__p_4 ROWS ONLY
```

גם ה-Summary מחושב ב-`GROUP BY` בבסיס הנתונים, כך שחוזרות 4 שורות לסטטוס ו-3 לעדיפות, ולא 100,000 רשומות.

**הגנות נוספות:**
- **`pageSize` מוגבל ל-100.**
- **רשימה מורשית של שדות מיון:** אין אפשרות למיין לפי עמודה שאין לה אינדקס תומך.
- **`Id` כ-tiebreaker בכל מיון:** בלעדיו, שורות עם ערך מיון זהה, למשל כ-55,000 פניות בסטטוס Completed, יכולות להופיע פעמיים או להיעלם בין עמודים. ה-`Id` כבר חלק מהמפתח של כל אינדקס non-clustered, ולכן ה-tiebreaker לא מוסיף Sort.

### האינדקסים ותרחישי החיפוש והסינון

| אינדקס | תרחיש | למה בסדר הזה |
|---|---|---|
| `IX_Requests_Status_CreatedAt` | סינון לפי סטטוס, החדשים קודם | Seek על `Status`, ואז `CreatedAt` כבר ממוין. משרת גם את `GROUP BY Status` |
| `IX_Requests_Priority_CreatedAt` | סינון לפי עדיפות, החדשים קודם | אותו עיקרון. משרת גם את `GROUP BY Priority` |
| `IX_Requests_AssignedTo_CreatedAt` | "התור של מטפל", מטפל + טווח תאריכים | Seek בשוויון על `AssignedTo`, ו-`CreatedAt` משמש גם כטווח וגם כמיון |
| `IX_Requests_OrganizationName_CreatedAt` | סינון לפי ארגון מדויק | אותו עיקרון |
| `IX_Requests_CreatedAt` | רשימה ללא סינון (ברירת המחדל), טווח תאריכים בלבד | Seek על טווח, ובסדר המיון |
| `IX_Requests_UpdatedAt` | מיון לפי "עודכן לאחרונה" | מונע Sort על כל הטבלה |
| `IX_RequestStatusHistory_RequestId_ChangedAt` | היסטוריית שינויים של פנייה | Seek לפי פנייה, כבר ממוין לפי זמן השינוי |

**מדידה על 100,000 רשומות** (`SET STATISTICS IO`, logical reads, שאילתות עם פרמטרים כפי ש-EF שולח):

| שאילתה | לפני האינדקסים החדשים (COUNT) | אחרי (COUNT / עמוד של 20) | Plan אחרי |
|---|---|---|---|
| לפי מטפל | 2,539 (Clustered Index Scan) | 34 / 196 | Index Seek |
| לפי ארגון | 2,539 (Clustered Index Scan) | 8 / 157 | Index Seek |
| טווח תאריכים | 40 | 40 / 154 | Index Seek (אינדקס קיים) |
| סטטוס + מטפל + טווח | 2,539 (Clustered Index Scan) | 15 / 685 | Seek על שני אינדקסים |

בכל שאילתות העמוד ה-Plan הוא `Top` מעל Index Seek שנקרא בסדר הפוך (`ORDERED BACKWARD`), כלומר בסדר המיון המבוקש. אין בו אופרטור Sort, והקריאה נעצרת אחרי 20 שורות.

**מגבלות ידועות:**
- **`search` לא יכול להשתמש באינדקס:** חיפוש חלקי מתורגם ל-`LIKE '%term%'`, שאינו sargable, ולכן נסרקים כל ה-`Title` וה-`OrganizationName`. ב-100,000 רשומות זה סביר. בהיקף גדול יותר החלופה היא Full-Text Search.
- **שילוב סטטוס + מטפל:** `Status` אינו חלק מ-`IX_Requests_AssignedTo_CreatedAt`, ולכן הוא נבדק רק אחרי Key Lookup לכל שורה (685 קריאות בטבלה למעלה). אפשר להוסיף `INCLUDE (Status)`, במחיר עדכון אינדקס נוסף בכל שינוי סטטוס.
- **אין מיון לפי `Title` / `OrganizationName`, בכוונה:** מיון על עמודות טקסט, במיוחד ב-offset עמוק, דורש Sort על כל הטבלה.

## בדיקות

- 9 בדיקות backend (xUnit)
- 22 בדיקות frontend (Vitest)
- סה"כ 31 בדיקות, כולן עוברות

## מה הייתי משפר עם יותר זמן

- **אימות והרשאות (Authentication/Authorization)** — עבור סביבת ריבוי-משתמשים אמיתית, כדי לשלוט בגישה למערכת ולפעולות השונות.
- **בדיקות אינטגרציה רחבות יותר** — בדיקות מול מופע SQL Server אמיתי, בנוסף לבדיקות הקיימות מול InMemory.
- **חיפוש טקסט מלא (Full-text search)** — אם מערך הנתונים יגדל משמעותית מעבר לגודלו הנוכחי, כדי לשפר את ביצועי החיפוש ולתת פתרון מתאים יותר ל-scale גדול.
- **Cache לנתוני ה-Summary** — אם כמות הקריאות ל-`GET /api/requests/summary` תגדל, הייתי שוקל לשמור את תוצאת ה-aggregation ב-Cache ולבצע invalidation לאחר שינוי בנתונים. בגודל הנוכחי לא ראיתי צורך להוסיף את המורכבות הזו.

## שימוש ב-AI
נעשה שימוש ב-Claude Code כחלק מתהליך הפיתוח לאורך המבדק, לסיוע במימוש והרחבת רכיבים ב-Backend וב-Angular, כתיבת בדיקות, איתור תקלות וביצוע Code Review. בנוסף, נעזרתי בו לבחינת חלופות ארכיטקטוניות, שיקולי ביצועים ומקרי קצה, למשל אופן הטיפול ב-Concurrency ואופן מימוש החיפוש מול בסיס הנתונים.

ה-AI שימש ככלי עבודה וביקורת לאורך הפיתוח, אך הבחירה בפתרון הסופי, ההבנה המלאה של הקוד והאחריות עליו הן שלי. לאחר כל שינוי משמעותי עברתי על הקוד ווידאתי שאני מבינה את המימוש ואת ההשפעה שלו על המערכת, ולאחר מכן בדקתי אותו באמצעות בדיקות אוטומטיות והרצה בפועל.