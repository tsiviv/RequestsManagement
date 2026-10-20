/*
  Measures the two improvements proposed in docs/PERFORMANCE.md against the current queries,
  and checks that each rewrite returns the same rows / count:
    1. Deep OFFSET page: deferred join (pick the page's Ids from the narrow index first).
    2. Free-text search COUNT: binary collation for the LIKE predicate.
  Run against the 100,000-row seed data set:
    sqlcmd -S "(localdb)\MSSQLLocalDB" -d RequestsManagement -i docs/perf/measure-improvements.sql -f 65001
*/
SET NOCOUNT ON;
SELECT 'collation: ' + CONVERT(nvarchar(100), DATABASEPROPERTYEX(DB_NAME(), 'Collation'));
DECLARE @p nvarchar(200) = N'@skip int, @take int, @search nvarchar(202)';
DECLARE @skip int = 50000, @take int = 20, @search nvarchar(202) = N'%חשבונית%';
DECLARE @deferred nvarchar(max) = N'
SELECT r.Id, r.Title, r.OrganizationName, r.AssignedTo, r.Status, r.Priority, r.CreatedAt, r.UpdatedAt, r.RowVersion
FROM Requests r
WHERE r.Id IN (SELECT Id FROM Requests ORDER BY CreatedAt DESC, Id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY)
ORDER BY r.CreatedAt DESC, r.Id DESC';
DECLARE @countBin nvarchar(max) = N'SELECT COUNT(*) FROM Requests WHERE Title COLLATE Latin1_General_100_BIN2 LIKE @search OR OrganizationName COLLATE Latin1_General_100_BIN2 LIKE @search';
DECLARE @count nvarchar(max) = N'SELECT COUNT(*) FROM Requests WHERE Title LIKE @search OR OrganizationName LIKE @search';
-- warm
EXEC sp_executesql @deferred, @p, @skip, @take, @search; EXEC sp_executesql @countBin, @p, @skip, @take, @search; EXEC sp_executesql @count, @p, @skip, @take, @search;
SET STATISTICS IO ON; SET STATISTICS TIME ON;
RAISERROR('=== deep page, deferred join', 0, 1) WITH NOWAIT; EXEC sp_executesql @deferred, @p, @skip, @take, @search;
RAISERROR('=== search COUNT, default collation', 0, 1) WITH NOWAIT; EXEC sp_executesql @count, @p, @skip, @take, @search;
RAISERROR('=== search COUNT, BIN2 collation', 0, 1) WITH NOWAIT; EXEC sp_executesql @countBin, @p, @skip, @take, @search;
SET STATISTICS IO OFF; SET STATISTICS TIME OFF;
-- same rows? (correctness of both rewrites)
DECLARE @a TABLE (Id int); DECLARE @b TABLE (Id int);
INSERT @a SELECT Id FROM Requests ORDER BY CreatedAt DESC, Id DESC OFFSET 50000 ROWS FETCH NEXT 20 ROWS ONLY;
INSERT @b SELECT Id FROM Requests WHERE Id IN (SELECT Id FROM Requests ORDER BY CreatedAt DESC, Id DESC OFFSET 50000 ROWS FETCH NEXT 20 ROWS ONLY);
SELECT 'deferred join same 20 rows: ' + CASE WHEN NOT EXISTS (SELECT Id FROM @a EXCEPT SELECT Id FROM @b) AND (SELECT COUNT(*) FROM @b) = 20 THEN 'yes' ELSE 'NO' END;
SELECT 'search counts default/BIN2: ' + CONVERT(varchar, (SELECT COUNT(*) FROM Requests WHERE Title LIKE N'%חשבונית%' OR OrganizationName LIKE N'%חשבונית%')) + ' / '
     + CONVERT(varchar, (SELECT COUNT(*) FROM Requests WHERE Title COLLATE Latin1_General_100_BIN2 LIKE N'%חשבונית%' OR OrganizationName COLLATE Latin1_General_100_BIN2 LIKE N'%חשבונית%'));
