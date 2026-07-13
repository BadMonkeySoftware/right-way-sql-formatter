-- Test: cursor VARIABLES (DECLARE @v CURSOR; SET @v = CURSOR ... FOR ...) vs classic cursors
DECLARE @kill_cursor CURSOR;
DECLARE @session_id INT;

SET @kill_cursor = CURSOR LOCAL FAST_FORWARD READ_ONLY
FOR

SELECT b.session_id
FROM #blockers AS b
WHERE b.wait_ms >= 1000;

OPEN @kill_cursor;

FETCH NEXT
FROM @kill_cursor
INTO @session_id;

WHILE @@FETCH_STATUS = 0
BEGIN
    FETCH NEXT
    FROM @kill_cursor
    INTO @session_id;
END;

CLOSE @kill_cursor;

DEALLOCATE @kill_cursor;

-- classic named cursor (regression: must keep CursorDeclaration structure)
DECLARE classic_cursor CURSOR
FOR
SELECT name
FROM sys.tables;

OPEN classic_cursor;

CLOSE classic_cursor;

DEALLOCATE classic_cursor;
