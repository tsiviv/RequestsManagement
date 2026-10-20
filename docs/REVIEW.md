# בדיקות סופיות ותיקונים (F9)

תוצאות ביצוע תוכנית הבדיקות שב-[WORK_PLAN.md](WORK_PLAN.md), סעיף F9. המסמך כולל:
- מטריצת עמידה בדרישות (T9.1).
- תוצאות הבדיקות האוטומטיות והכלים (T9.2–T9.5).
- בדיקה מקצה לקצה בדפדפן (T9.6).
- בדיקת הביצועים החוזרת (T9.7).
- רשימת הממצאים והתיקונים (T9.8).

## 1. מטריצת עמידה בדרישות (T9.1)

לכל דרישה: איפה היא ממומשת, איזו בדיקה מכסה אותה ואיפה היא מתועדת. **U** = בדיקת Unit (`RequestsManagement.Tests`), **I** = Integration מול SQL Server (`RequestsManagement.IntegrationTests`), **A** = Angular (Vitest), **E** = בדיקה בדפדפן (T9.6).

| דרישה | מימוש | בדיקות | תיעוד |
|---|---|---|---|
| מבנה פנייה, כולל `AssignedTo` ו-`RowVersion` | `Models/RequestEntity.cs`, `Data/RequestConfiguration.cs` | כל בדיקות ה-I (סכמה אמיתית מה-Migrations) | README › Database |
| 100,000 רשומות + יצירה חוזרת | `Data/RequestSeeder.cs`, `--reseed` ב-`Program.cs` | נבדק בהרצה (דטרמיניזם: checksum זהה בשתי הרצות) | README › יצירה חוזרת |
| Pagination בצד השרת | `RequestService.GetRequestsAsync` (Deferred Join + `OFFSET/FETCH`) | U `AppliesPagination`, I `PagingThroughEveryPage_ReturnsEachRowExactlyOnce` | README › איך נמנעת טעינת כלל הנתונים |
| סינון משולב (Status, Priority, OrganizationName, AssignedTo, טווח תאריכים) | `RequestService.GetRequestsAsync` | U `CombinesOrganizationAssigneeAndHalfOpenDateRange`, I `CombinedFilters_SortAndPaging_ReturnExactlyTheMatchingPage`, A פילטרים | README › טבלת פרמטרים |
| חיפוש ב-Title וב-OrganizationName | `Title.Contains || OrganizationName.Contains` | I `FreeTextSearch_MatchesTitleOrOrganization_AsSqlLike` (כולל `%` כתו רגיל), A debounce | README, PERFORMANCE › Bottleneck 1 |
| מיון לפי 3 שדות לפחות, Asc/Desc | `ApplySort`: `createdAt` / `updatedAt` / `organizationName` / `priority` / `id` | U `SortingByOrganization…`, `SortingByPriority…`, `SortingById…`, `SortingByAFieldThatIsNotSortable_IsRejected`; I `EmptySortParameters_FallBackToTheDefaultOrder` | README › מיון |
| 2 Aggregations לפחות | Summary: סה"כ, לפי סטטוס ולפי עדיפות | U `GetSummaryAsync_ReturnsCountsByStatusAndPriority`, A `displays the aggregations`, E | README, PERFORMANCE › פעולה 2 |
| הגבלת Page Size ו-Validation | `RequestQueryDto` (`[Range]`, `[MaxLength]`), `ValidateSort`, `ValidateDateRange` | I `InvalidQuery_Returns400ProblemDetails` (8 מקרים, כולל `pageSize` 0/101) | README › טבלת פרמטרים |
| `CancellationToken` בפעולות I/O | כל Action ב-Controller, עד `ToListAsync` / `SaveChangesAsync` | U `ClientCancelledRequest_IsLoggedAsInformation…`, E ביטול בקשה (`ERR_ABORTED`) | README › Logging |
| DTOs ולא ישויות | `DTOs/*`, `Mapping/RequestMappings.cs` | כל בדיקות ה-I מפענחות DTOs | README › מבנה |
| תיעוד מניעת טעינה לזיכרון ואינדקסים | – | – | README › שליפת נתונים, PERFORMANCE |
| פנייה לא קיימת → 404 | `NotFoundException` → middleware | U `WhenRequestDoesNotExist…`, I `UnknownRequest_Returns404_ForUpdateAndHistory` | README |
| סטטוס לא חוקי / מעבר לא מותר → Validation | `StatusTransitionPolicy`, `ValidateTargetStatus` | U כל 16 צירופי המעברים, I `ForbiddenTransition_Returns400…`, `SameStatus_Returns400`, `MalformedUpdateBody…` | README |
| Optimistic Concurrency: רק עדכון אחד מצליח | `rowversion` + `OriginalValue`, `DbUpdateConcurrencyException` → 409 | I `ConcurrentUpdatesWithTheSameRowVersion_ExactlyOneSucceeds` (8 מקבילים), `SecondUpdateWithTheSameRowVersion_Gets409…`, E 409 | README › Concurrency |
| UpdatedAt ו-RowVersion מתעדכנים בעקביות | `TrySaveStatusChangeAsync` (אותו timestamp ל-`UpdatedAt` ול-`ChangedAt`) | I `SecondUpdate…` (RowVersion חדש מה-DB), I `EachChange_IsRecorded…` (`ChangedAt` == `UpdatedAt`) | README |
| אין Lost Update | בדיקת RowVersion מוקדמת + בדיקה ב-`UPDATE ... WHERE RowVersion` | I `SecondUpdateWithTheSameRowVersion…`. אומת ב-Mutation: הסרת ההגנה מכשילה את הבדיקה | README |
| Audit לכל שינוי סטטוס (RequestId, Previous, New, ChangedAt, ChangedBy) | `RequestStatusHistoryEntity`, נשמר באותה טרנזקציה | U `StatusHistoryTests`, I `ConflictDetectedOnlyAtSave_RollsBackTheAuditRowWithTheUpdate` | README |
| Endpoint להיסטוריה | `GET /api/requests/{id}/history` | U `GetHistoryAsync_*`, I `EachChange_IsRecorded…`, A dialog, E | README › API |
| Bulk עד 100, התנהגות מוגדרת ומנומקת | `BulkUpdateStatusAsync` (Partial Success) | U `BulkUpdateTests`, `BulkUpdateContractTests`; I `MixedBatch…`, `OneHundredItems_AllUpdated`, `BulkWithMoreThan100Items…`, `BulkWithDuplicateIds…`; E | README › חוזה Bulk |
| Angular: חיפוש, סינון, מיון ודפדוף בצד השרת | `RequestsService`, `requests-page.component.ts` | A (פרמטרים לשרת), E | README |
| Debounce לחיפוש | `request-filters.component.ts` (300 ms) | A `debounces search input…`, E (7 אותיות → בקשה אחת) | – |
| ביטול/החלפת בקשות קודמות | `switchMap` ב-`requests$` | A `cancels a still-running request…` (Mutation ל-`mergeMap` מכשיל), E (`ERR_ABORTED`) | – |
| Loading / Empty / Error | `requests-page.component.html`, dialog, summary | A (3 מצבים + summary), E | – |
| עדכון סטטוס וטיפול ב-409 | `onStatusChange`, `http-error.interceptor.ts` | A `handles a 409…`, E | – |
| הצגת Aggregations | `request-summary.component` | A `displays the aggregations…`, E (תואם ל-API אחרי עדכונים) | – |
| צפייה בהיסטוריה | `request-history-dialog.component` | A (4 מצבים), E | – |
| הפרדה בין Components ל-Services | Components מציגים, `RequestsService` / `CurrentUserService` ניגשים ל-API | Review (T9.3) | README › מבנה |
| ביצועים: 2 פעולות, Plan, אינדקסים, Bottleneck | `docs/perf/*.sql` | הרצה חוזרת (T9.7) | PERFORMANCE |
| Cache: Expiration, Invalidation, נימוק, Scale-out | `MemoryRequestSummaryCache` (גרסה במפתח) | U `SummaryCacheTests` (כולל מרוץ, Mutation), I `Summary_IsFreshRightAfterSingleAndBulkUpdates` | README › Cache |
| 4 בדיקות לפחות, אחת Integration | 117 U + 48 I + 72 A | – | README › בדיקות |
| תכנון עבודה | – | – | WORK_PLAN |
| Global Error Handling / ProblemDetails | `ExceptionHandlingMiddleware` | U `LoggingTests`, I (כל תגובות השגיאה הן `application/problem+json`) | README |
| Logging ללא מידע רגיש | `RequestService`, middleware | U `SuccessfulUpdate_…ButNotTheUser`, `ClientErrorEchoingUserInput_CannotForgeLogLines` | – |

**תוצאה:** לכל דרישה יש מימוש ובדיקה. התיעוד של Angular ושל ה-Logging מופיע כרגע רק בתוך הקוד, ונכנס ל-README בשלב F10 (T10.1).

## 2. כלים ובדיקות אוטומטיות (T9.2–T9.5)

| בדיקה | תוצאה |
|---|---|
| `dotnet build -warnaserror` | עובר, אחרי תיקון F-1 |
| `dotnet format --verify-no-changes` | נקי, אחרי תיקון F-2 |
| `dotnet list package --vulnerable --include-transitive` | אין חבילות פגיעות (3 הפרויקטים) |
| `npm audit` | 0 פגיעויות, אחרי תיקון F-3 (היו 14: 3 Critical, 8 High) |
| `ng build --configuration production` | עובר. אזהרת Budget (643 kB לעומת 500 kB) קיימת מלפני העבודה |
| בדיקות Backend, שתי הרצות ברצף | 117 U + 48 I עוברות בשתיהן. אין בדיקות לא יציבות |
| בדיקות Frontend, שתי הרצות ברצף | 72 עוברות בשתיהן |
| Coverage (Unit) | `RequestService` 96% שורות / 93% ענפים; `StatusTransitionPolicy`, `MemoryRequestSummaryCache`, `ExceptionHandlingMiddleware`: 100% |
| Coverage (Integration) | `RequestsController` 100% |
| חיפוש `innerHTML` / `bypassSecurityTrust` | אין |
| Subscriptions | זרמים ארוכים עם `takeUntilDestroyed`. קריאות HTTP חד-פעמיות נסגרות לבד |
| SQL דינמי | רק `DBCC CHECKIDENT` קבוע ב-Seeder. אין אינטרפולציה של קלט |
| סודות בריפו | אין. ה-connection string הוא LocalDB עם Windows Authentication |
| CORS | מוגבל ל-`http://localhost:4200`, ורק ב-Development |

## 3. בדיקה מקצה לקצה בדפדפן (T9.6)

**הסביבה:**
- **דפדפן:** Microsoft Edge (headless) דרך `playwright-core`.
- **ה-UI:** הקוד הנוכחי, מוגש על ידי `ng serve`.
- **ה-API:** מופע עם הקוד הנוכחי מול מסד זמני של 100,000 רשומות. קריאות ה-API של הדף נותבו אליו.
- **בידוד:** כל תרחיש מתחיל בטעינת דף חדשה.

**תוצאה: 12/12.**

| # | תרחיש | תוצאה |
|---|---|---|
| 1 | טעינה ראשונית | בקשת רשימה אחת, 25 שורות, Aggregations (100,000), תאריכים בפורמט 24 שעות |
| 2 | חיפוש עם Debounce | הקלדת 7 אותיות → בקשה אחת בלבד, וכל השורות תואמות |
| 3 | שינוי סינון מהיר | הבקשה הקודמת, שעוד הייתה בדרך, בוטלה (`net::ERR_ABORTED`), וכל השורות בסטטוס החדש |
| 4 | מיון ודפדוף | נשלחים לשרת (`sortBy=organizationName`, ואחריו `page=2`) |
| 5 | Empty State | ההודעה מוצגת, ו"איפוס מסננים" מחזיר את הנתונים |
| 6 | Error State | ההודעה וכפתור "נסה שוב" מוצגים, והניסיון החוזר מצליח |
| 7 | עדכון סטטוס | מוצעים רק המעברים המותרים (חדש → בטיפול), העדכון מצליח וה-Summary מתרענן |
| 8 | דיאלוג היסטוריה | נפתח מכל שורה, עם מצב Empty תקין. תוכן ההיסטוריה עצמו מכוסה בבדיקות ה-I |
| 9 | 409 | משתמש אחר עדכן קודם. ה-UI מסביר וטוען מחדש, ובהיסטוריה נרשם רק השינוי של המשתמש האחר |
| 10 | Bulk | בחירת 3 שורות, הסרגל, "עודכנו 3 מתוך 3", ו-Aggregation בטיפול זהה ל-API |
| 11 | עדכון בלי שם משתמש | נחסם עם הסבר, ושום בקשה לא נשלחת |
| 12 | מסך צר (400px) | אין גלילה אופקית של הדף. הטבלה גוללת בתוך המיכל שלה |

## 4. ביצועים (T9.7)

הרצה חוזרת של [perf/measure-queries.sql](perf/measure-queries.sql) ושל [perf/measure-improvements.sql](perf/measure-improvements.sql) על מסד חדש עם 100,000 רשומות:
- **אותן קריאות כמו במסמך** לכל שאילתה עם אינדקס, כולל מיון לפי עדיפות (71 קריאות, כמו שאר המיונים הנתמכים באינדקס).
- **ה-Deferred Join יציב:** 320 קריאות (319 במדידה המקורית), ואותן 20 שורות.
- **החריגה היחידה:** ה-Plan של OFFSET רגיל, שבחר הפעם Scan + Sort (ראו F-7).
- **אין N+1:** ה-Bulk טוען את כל הפניות בשאילתה אחת, וההיסטוריה היא Seek אחד.

## 5. ממצאים ותיקונים (T9.8)

| ID | בדיקה | חומרה | ממצא | תיקון | אימות |
|---|---|---|---|---|---|
| F-1 | T9.2 | Low | אזהרת nullable (CS8602) בבדיקת אינטגרציה מכשילה `-warnaserror` | `Assert.NotNull` לפני השימוש | build נקי |
| F-2 | T9.2 | Low | פורמט רווחים וסופי שורות ב-`RequestServiceTests.cs` | `dotnet format whitespace` | `--verify-no-changes` נקי |
| F-3 | T9.4 | **High** | 14 פגיעויות npm. בתלויות ייצור: High אחת ב-`@angular/router` (DoS ב-SSR. האפליקציה לא משתמשת ב-SSR, אבל החבילה נשלחת). השאר בכלי הבנייה (`@angular/build` → `piscina`, `undici`) | עדכון Angular ל-21.2.25 ולכלי הבנייה ל-21.2.27, באותו קו גרסאות. ה-lock נבנה מחדש. `npm audit fix` נכשל בבאג של npm 9/10 (`edgesOut`), ולכן העדכון נעשה עם npm 11 בעותק נפרד ונבדק שם | `npm audit`: 0. build ו-71 בדיקות עוברים על הגרסאות החדשות. `npm install` מה-lock החדש עובד גם עם npm 9.6.7 |
| F-4 | T9.3 | Low | `$any($event.target)` בשדה השם | Template reference (`#nameInput`) עם טיפוס מלא | 3 בדיקות השדה עוברות |
| F-5 | T9.2 | Medium | `?sortBy=` או `?sortDirection=` ריקים מחזירים 400 ("The SortBy field is required") במקום מיון ברירת המחדל. ערך ריק נקשר כ-`null`, ו-`string` שאינו nullable הוא Required מרומז | `SortBy` / `SortDirection` הפכו ל-`string?`, וערך חסר או ריק מתפרש כברירת המחדל במקום אחד | בדיקה חדשה, I `EmptySortParameters_FallBackToTheDefaultOrder` (4 מקרים). נכשלה על הקוד הקודם ועוברת אחרי התיקון |
| F-6 | T9.4 | Medium | Log Injection: הודעת ה-400 נרשמת בלוג, וחלק מההודעות מצטטות קלט (`Invalid sortBy '…'`), כך ש-`%0A` יוצר שורת לוג מזויפת | תווי בקרה מוחלפים ברווח לפני הרישום. ההודעה ללקוח לא משתנה | בדיקה חדשה, U `ClientErrorEchoingUserInput_CannotForgeLogLines` |
| F-7 | T9.7 | Medium | `PERFORMANCE.md` הציג את עלות ה-OFFSET הרגיל (153,454 קריאות) כקבועה. במדידה החוזרת ה-Optimizer בחר Plan אחר (Scan + Sort, 2,539) | הערה במסמך שה-Plan של OFFSET רגיל תלוי בסטטיסטיקות, ושה-Deferred Join יציב (319 / 320) | מדידה חוזרת |
| F-8 | T9.5 | Low | מקרה קצה `pageSize=0` לא נבדק (רק 101) | נוסף ל-`InvalidQuery_Returns400ProblemDetails` | עוברת |
| F-9 | T9.6 | Low | התאריכים בטבלה הוצגו בפורמט 12 שעות עם AM/PM באנגלית ("PM 11:54") בממשק עברי, ולא תאמו את דיאלוג ההיסטוריה | פורמט אחיד `d MMM y, HH:mm` | נראה בצילום מסך. נבדק בתרחיש 1 |
| F-10 | T9.6 | Medium | נגישות: ה-`aria-label` של בחירת הסטטוס בכל שורה ("עדכון סטטוס, נוכחי: …, עבור …") לא הגיע ל-DOM. הוא הוגדר כ-`[attr.aria-label]`, ו-`MatSelect` דורס אותו בערך `null` כשיש לו `aria-labelledby`. קורא מסך שמע רק "חדש", בלי לדעת לאיזו פנייה הבחירה שייכת | קישור דרך ה-input של `MatSelect` (`[aria-label]`) | בדיקה חדשה, A `names the select after the request…`. נכשלת על הקוד הקודם |

### סבב שני: עברית, נגישות לבודקים והשוואה מלאה מול האפיון

| ID | בדיקה | חומרה | ממצא | תיקון | אימות |
|---|---|---|---|---|---|
| F-11 | UX | Medium | הודעות השגיאה של השרת היו באנגלית ("Cannot change status from 'New'…"), בממשק עברי | כל ההודעות ללקוח בעברית, עם שמות הסטטוסים בעברית (`RequestStatus.DisplayName()`): הודעות האפליקציה, ה-DTOs, הודעות ה-Model Binding של ASP.NET (`ValidationMessagesExtensions`) וכותרות ה-ProblemDetails. JSON לא תקין מקבל הודעה כללית בעברית, וההודעה הפנימית של System.Text.Json לא נחשפת. הלוגים נשארים באנגלית, כי הם למפתחים | בדיקה חדשה, I `EveryErrorResponse_IsInHebrew`, על 13 סוגי שגיאה |
| F-12 | UX | Low | בבקשה עם גוף לא תקין נוספה לשגיאה העברית כפילות באנגלית: "The dto field is required." (Required מרומז של ASP.NET לפרמטר non-nullable) | `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes`. לכל שדה חובה ב-DTOs יש `[Required]` מפורש עם הודעה בעברית | אותה בדיקה (3 מקרי גוף לא תקין) |
| F-13 | נגישות לבודקים | Medium | Swagger לא הציג את הכותרת `X-User-Name`, כך שעדכון מ-"Try it out" החזיר תמיד 400 | `UserNameHeaderOperationFilter`: הכותרת מוצגת כחובה, עם ערך ברירת מחדל, בפעולות שמשנות נתונים בלבד | בדיקה חדשה, I `Swagger_DocumentsTheUserHeader_OnChangingOperationsOnly` |
| D-1 | תיעוד (סעיף 11) | Medium | אין נימוק לבחירת בסיס הנתונים | סעיף "בחירת בסיס הנתונים", עם חלופות (MongoDB, PostgreSQL) | – |
| D-2 | תיעוד (סעיף 11) | Medium | רק החלטה טכנולוגית אחת סומנה ככזו | סעיף "החלטות טכנולוגיות וחלופות" עם שתי החלטות: Optimistic Concurrency, ו-Partial Success | – |
| D-3 | תיעוד | Low | כותרת לא עדכנית ממבדק קודם ("האתגר שנבחר") | הוחלפה בסעיף "Concurrency ומניעת Lost Update" שמתאר את המימוש בפועל | – |
| D-4 | תיעוד (סעיף 11) | Medium | פירוט השימוש ב-AI היה כללי | נוספו דוגמאות קונקרטיות: מה הוחלט ידנית, ומה נבדק ולא התקבל כמו שהוא | – |
| D-5 | הוראות הרצה | Medium | חסרה גרסת Node, ו-Angular 21 דורש 20.19+ / 22.12+. גם הגרסאות המדויקות של שאר הטכנולוגיות חסרו | טבלת טכנולוגיות וגרסאות, ודרישות מקדימות מפורטות | הגרסאות נבדקו מול `package.json` וקבצי ה-csproj |
| D-6 | נגישות לבודקים | Low | ה-README לא קישר למסמכי התכנון, הביצועים והבדיקה | קישורים בראש ה-README. נוספו גם סעיפי Angular, טיפול בשגיאות ו-Logging, ומגבלות ידועות | כל הקישורים נבדקו |

**ממצאים שנשארים פתוחים, עם נימוק:**
- **אזהרת Bundle Budget (Low):** 643 kB לעומת 500 kB. האזהרה הייתה קיימת עוד לפני העבודה, כ-616 kB ב-commit ההתחלתי. מקורה בעיקר ב-Angular Material, ולהגדלת התקציב או לפיצול ה-bundle אין ערך מבחינת הדרישות.
- **זהות מבוססת Header (Medium, בהחלטה D2):** אין Authentication, ו-`X-User-Name` אינו מאומת. זו מגבלה ידועה, ומתועדת ב-README.

**פעולה נדרשת בסביבה המקומית:** ה-`node_modules` המקומי עדיין בגרסאות הישנות, כי `ng serve` רץ ממנו. אחרי עצירת `ng serve` מריצים `npm install` בתיקייה `ui/`.
