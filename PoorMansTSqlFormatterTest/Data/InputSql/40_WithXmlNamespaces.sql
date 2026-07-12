-- Test: WITH XMLNAMESPACES as CTE-clause prefix (standalone and combined with a CTE)
WITH XMLNAMESPACES ('http://schemas.microsoft.com/sqlserver/2004/07/showplan' AS p)
UPDATE b SET b.col = 1 FROM dbo.SomeTable AS b WHERE b.id = 2;

WITH XMLNAMESPACES('http://example.com/ns' AS x), source AS (SELECT 1 AS n)
SELECT s.n FROM source AS s;

WITH XMLNAMESPACES (DEFAULT 'http://example.com/default')
SELECT c.q FROM dbo.XmlTable AS c;

WITH XMLNAMESPACES ('http://example.com/ns' AS p)
INSERT INTO #results (a, b)
SELECT 1, 2;
