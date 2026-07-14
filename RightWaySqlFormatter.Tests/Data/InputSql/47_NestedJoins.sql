-- Test: nested-join chained ON sections (upstream #288/#241/#30).
-- Each subsequent ON closes the enclosing join; JoinOn sections are siblings.
SELECT 1
FROM toto
INNER JOIN titi
LEFT JOIN tata
	ON 2 = 2
		ON 1 = 1

SELECT [1].*, [2].*
FROM @TestTable [1]
LEFT JOIN @TestTable [2]
INNER JOIN @TestTable [3] ON [2].TestColumn2 = [3].TestColumn2
      AND 1 < 2
      ON [1].TestColumn1 = [2].TestColumn1

SELECT [1].*, [2].*
FROM @TestTable [1]
LEFT JOIN (@TestTable [2]
INNER JOIN @TestTable [3] ON [2].TestColumn2 = [3].TestColumn2
      AND 1 < 2)
      ON [1].TestColumn1 = [2].TestColumn1

INSERT INTO #tt3 WITH(TABLOCK) (a, b, c) SELECT
T1.x, T3.y, T2.z
FROM dbo.D1 T1
LEFT OUTER JOIN dbo.D2 T2
INNER JOIN dbo.D3 T3
ON (T3.k = T2.k)
ON (T2.k = T1.k)

SELECT *
FROM a
JOIN b
JOIN c
JOIN d ON d.k = c.k ON c.k = b.k ON b.k = a.k
