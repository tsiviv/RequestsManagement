# ביצועים

בדיקת היעילות של שתי פעולות ה-API המרכזיות על 100,000 רשומות: Execution Plans, האינדקסים שנוספו ולמה, וה-Bottlenecks שנמצאו.

## שיטה

- **נתונים:** 100,000 פניות מה-Seeder הדטרמיניסטי, על SQL Server LocalDB.
- **"לפני / אחרי" אינדקסים:** כל שאילתה, בצורה ש-EF Core מייצר, רצה כמו שהיא ועם `WITH (INDEX(0))`, שכופה סריקה מלאה של הטבלה.
- **מדדים:** `STATISTICS IO` (logical reads, המדד היציב), `STATISTICS TIME` והאופרטורים ב-Plan בפועל. הסקריפטים: [perf/measure-queries.sql](perf/measure-queries.sql), [perf/measure-improvements.sql](perf/measure-improvements.sql).

## תוצאות

### `GET /api/requests` (רשימה)

| תרחיש | עם אינדקסים | Plan | בלי אינדקסים | Plan |
|---|---|---|---|---|
| ברירת מחדל, עמוד 1 (החדשים קודם) | **71** reads, 2 ms | Index Scan מסודר + 20 Key Lookups, בלי Sort | 2,539 reads, 394 ms | Clustered Scan + **Sort** |
| מטפל + סטטוס New + יולי | **386** reads, 2 ms | Index Seek | 2,539 reads, 46 ms | Clustered Scan + Sort |
| ↳ `COUNT` | **17** reads, 4 ms | Seek על שני אינדקסים + Hash Match | 2,539 reads, 45 ms | Clustered Scan |
| סינון לפי ארגון | **71** reads, 2 ms | Index Seek | 2,539 reads, 61 ms | Clustered Scan + Sort |
| מיון לפי ארגון, עמוד 1 | **71** reads, 2 ms | Index Scan מסודר, בלי Sort | 2,539 reads, 1,400 ms | Clustered Scan + Sort |
| מיון לפי עדיפות, עמוד 1 | **71** reads | Index Scan בסדר הפוך, בלי Sort | 2,539 reads | Clustered Scan + Sort |
| חיפוש טקסט חופשי, עמוד | 606 reads, 5 ms | Index Scan לפי תאריך עד 20 התאמות | 2,539 reads, 1,093 ms | Clustered Scan + Sort |
| ↳ `COUNT` לחיפוש | 2,539 reads, **528–1,106 ms** | Clustered Scan, אין אינדקס שעוזר | 2,539 reads, 1,060 ms | Clustered Scan |
| `COUNT` ללא סינון | 536 reads, **60–71 ms** | Index Scan של האינדקס הצר ביותר | 2,539 reads, 61 ms | Clustered Scan |
| עמוד עמוק 2,501 (OFFSET 50,000) | **153,454** reads, 335 ms | Index Scan + **50,020 Key Lookups** | 2,539 reads, 395 ms | Clustered Scan + Sort |

### `GET /api/requests/summary` (Aggregations, כשהערך לא ב-Cache)

| שאילתה | עם אינדקסים | בלי אינדקסים |
|---|---|---|
| `COUNT(*)` | 536 reads, 60 ms (Index Scan) | 2,539 reads, 66 ms |
| `GROUP BY Status` | 625 reads, 86 ms (Index Scan + Stream Aggregate) | 2,539 reads, 101 ms (Hash Match) |
| `GROUP BY Priority` | 621 reads, 80 ms (Index Scan + Stream Aggregate) | 2,539 reads, 92 ms (Hash Match) |

סה"כ כ-230 ms לחישוב, ולכן ה-Summary נשמר ב-Cache (ראו README).

**מה רואים:** תרחישי הסינון והמיון הנפוצים יורדים מסריקה של כל הטבלה (2,539 עמודים, עד 1.4 שניות) ל-17–386 עמודים ו-2–5 ms, ובלי Sort ב-Plan, כי האינדקס כבר בסדר המבוקש.

## האינדקסים ולמה

| אינדקס | נוסף בשביל |
|---|---|
| `IX_Requests_Status_CreatedAt` | סינון לפי סטטוס, החדשים קודם. `GROUP BY Status` |
| `IX_Requests_Priority_CreatedAt` | סינון ומיון לפי עדיפות. `GROUP BY Priority` |
| `IX_Requests_AssignedTo_CreatedAt` | סינון לפי מטפל, ומטפל + טווח תאריכים |
| `IX_Requests_OrganizationName_CreatedAt` | סינון ומיון לפי ארגון |
| `IX_Requests_CreatedAt` | רשימת ברירת המחדל וטווח תאריכים |
| `IX_Requests_UpdatedAt` | מיון לפי עדכון אחרון |
| `IX_RequestStatusHistory_RequestId_ChangedAt` | היסטוריית פנייה, בלי Sort |

## Bottlenecks

### 1. עמוד עמוק: OFFSET 50,000 קורא 153,454 עמודים (מומש)

- **הבעיה:** `OFFSET` קורא ומשליך את כל השורות שלפני העמוד, ועל כל אחת מהן מתבצע Key Lookup לטבלה. זה פי 60 יותר קריאות מסריקה מלאה, וזה גדל עם הטבלה.
- **הפתרון, Deferred Join:** קודם בוחרים את 20 המזהים של העמוד מתוך האינדקס בלבד, ורק אז שולפים את 20 השורות המלאות (`WHERE Id IN (SELECT Id ... OFFSET ... FETCH ...)`).
- **התוצאה:** 153,454 → **319** קריאות, 335 → **13 ms**, אותן שורות. בעמוד הראשון העלות שווה או נמוכה יותר (63 לעומת 71), ולכן זה המסלול לכל העמודים.
- **חלופה שלא נבחרה, Keyset Pagination:** עלות קבועה בכל עומק, אבל בלי קפיצה לעמוד שרירותי, כך שה-API וה-UI היו משתנים.

### 2. `COUNT` של חיפוש טקסט חופשי: עד שנייה (לא מומש)

- **הבעיה:** `LIKE '%x%'` אינו sargable, ולכן ה-`COUNT` עובר על כל 100,000 השורות (528–1,106 ms). את העמוד עצמו מוצאים מהר (5 ms).
- **שיפור שנמדד:** collation בינארי בתנאי (`COLLATE Latin1_General_100_BIN2`): 528 → **117 ms**, אותן 10,968 תוצאות.
- **למה לא מומש:** החיפוש היה הופך לרגיש לאותיות גדולות/קטנות בטקסט באנגלית. זו החלטת מוצר.
- **לטווח ארוך:** Full-Text Search, כשהטבלה גדלה בסדר גודל.
