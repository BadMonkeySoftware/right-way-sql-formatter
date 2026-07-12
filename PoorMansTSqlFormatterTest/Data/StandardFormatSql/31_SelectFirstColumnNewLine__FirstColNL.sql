-- Test file for SelectFirstColumnOnNewLine option
SELECT
    col1
    ,col2
    ,col3
FROM dbo.MyTable

SELECT
    a.col1
    ,a.col2
    ,b.col3
FROM dbo.TableA a
INNER JOIN dbo.TableB b ON a.id = b.id
WHERE a.x = 1

SELECT
    col1
FROM SingleColTable
