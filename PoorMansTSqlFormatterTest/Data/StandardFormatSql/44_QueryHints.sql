-- Test: join hints inside OPTION(...) must not be mistaken for MERGE statements
SELECT p.a
FROM p
JOIN u ON u.i = p.i
OPTION (MERGE JOIN);

SELECT 1
OPTION (
    HASH JOIN
    ,FAST 10
    ,RECOMPILE
    );

-- regression: a real MERGE statement
MERGE INTO t
USING s
    ON t.id = s.id
WHEN MATCHED
    THEN
        UPDATE
        SET t.x = s.x;
