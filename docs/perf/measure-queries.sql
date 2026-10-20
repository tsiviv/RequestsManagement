/*
  Performance measurement for the two main API operations on the 100,000-row seed data set:
    1. GET /api/requests          (list: filter + sort + page, plus its COUNT)
    2. GET /api/requests/summary  (aggregations; the cost of a cache miss)

  Each query is the shape EF Core generates (parameterized via sp_executesql), and runs twice:
    - "indexed":   as the application sends it.
    - "no index":  with WITH (INDEX(0)), forcing a full clustered scan, i.e. what the same query
                   costs without the non-clustered indexes. No schema change needed.
  Each variant runs twice and the second (warm) run is the one reported, so disk reads do not
  distort the comparison.

  Run against a database seeded with `dotnet run -- --reseed`:
    sqlcmd -S "(localdb)\MSSQLLocalDB" -d RequestsManagement -i docs/perf/measure-queries.sql -f 65001
  Output: per query, STATISTICS IO (logical reads), STATISTICS TIME (CPU / elapsed) and the
  operators of the actual plan (STATISTICS PROFILE).
*/
SET NOCOUNT ON;

DECLARE @cols nvarchar(400) =
    N'SELECT Id, Title, OrganizationName, AssignedTo, Status, Priority, CreatedAt, UpdatedAt, RowVersion FROM {T} ';
DECLARE @params nvarchar(400) =
    N'@assignedTo nvarchar(100), @organization nvarchar(200), @from datetime2, @to datetime2, @search nvarchar(202), @skip int, @take int';

DECLARE @assignedTo nvarchar(100) = N'דנה כהן',
        @organization nvarchar(200) = N'רכבת ישראל - סניף חיפה',
        @from datetime2 = '2026-07-01', @to datetime2 = '2026-08-01',
        @search nvarchar(202) = N'%חשבונית%',
        @take int = 20;

DECLARE @queries TABLE (n int IDENTITY, label nvarchar(100), skip int, sql nvarchar(max));
INSERT @queries (label, skip, sql) VALUES
 (N'L1 list: default (newest first), page 1 - page query', 0,
  @cols + N'ORDER BY CreatedAt DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY'),
 (N'L1 list: default - COUNT', 0,
  N'SELECT COUNT(*) FROM {T}'),
 (N'L2 list: deep page 2,501 (OFFSET 50,000) - page query', 50000,
  @cols + N'ORDER BY CreatedAt DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY'),
 (N'L3 list: handler + status New + July - page query', 0,
  @cols + N'WHERE AssignedTo = @assignedTo AND Status = 0 AND CreatedAt >= @from AND CreatedAt < @to ORDER BY CreatedAt DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY'),
 (N'L3 list: handler + status New + July - COUNT', 0,
  N'SELECT COUNT(*) FROM {T} WHERE AssignedTo = @assignedTo AND Status = 0 AND CreatedAt >= @from AND CreatedAt < @to'),
 (N'L4 list: organization filter - page query', 0,
  @cols + N'WHERE OrganizationName = @organization ORDER BY CreatedAt DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY'),
 (N'L5 list: sorted by organization, page 1 - page query', 0,
  @cols + N'ORDER BY OrganizationName, CreatedAt, Id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY'),
 (N'L7 list: sorted by priority (most urgent first), page 1 - page query', 0,
  @cols + N'ORDER BY Priority DESC, CreatedAt DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY'),
 (N'L6 list: free-text search - page query', 0,
  @cols + N'WHERE Title LIKE @search OR OrganizationName LIKE @search ORDER BY CreatedAt DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY'),
 (N'L6 list: free-text search - COUNT', 0,
  N'SELECT COUNT(*) FROM {T} WHERE Title LIKE @search OR OrganizationName LIKE @search'),
 (N'S1 summary: total', 0,
  N'SELECT COUNT(*) FROM {T}'),
 (N'S1 summary: by status', 0,
  N'SELECT Status, COUNT(*) FROM {T} GROUP BY Status'),
 (N'S1 summary: by priority', 0,
  N'SELECT Priority, COUNT(*) FROM {T} GROUP BY Priority');

DECLARE @n int = 1, @max int = (SELECT MAX(n) FROM @queries);
DECLARE @label nvarchar(100), @variantLabel nvarchar(20), @skip int, @template nvarchar(max), @sql nvarchar(max), @variant int;

WHILE @n <= @max
BEGIN
    SELECT @label = label, @skip = skip, @template = sql FROM @queries WHERE n = @n;

    SET @variant = 1;
    WHILE @variant <= 2
    BEGIN
        SET @sql = REPLACE(@template, N'{T}', CASE @variant WHEN 1 THEN N'Requests' ELSE N'Requests WITH (INDEX(0))' END);

        -- Warm-up run, not measured.
        EXEC sp_executesql @sql, @params, @assignedTo, @organization, @from, @to, @search, @skip, @take;

        SET @variantLabel = CASE @variant WHEN 1 THEN N'indexed' ELSE N'no index' END;
        RAISERROR(N'=== %s | %s', 0, 1, @label, @variantLabel) WITH NOWAIT;
        SET STATISTICS IO ON; SET STATISTICS TIME ON; SET STATISTICS PROFILE ON;
        EXEC sp_executesql @sql, @params, @assignedTo, @organization, @from, @to, @search, @skip, @take;
        SET STATISTICS PROFILE OFF; SET STATISTICS TIME OFF; SET STATISTICS IO OFF;

        SET @variant += 1;
    END

    SET @n += 1;
END
