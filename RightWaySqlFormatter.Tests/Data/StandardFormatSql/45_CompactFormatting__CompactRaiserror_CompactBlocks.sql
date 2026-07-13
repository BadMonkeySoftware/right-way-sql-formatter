-- Test: compact formatting options (RAISERROR argument lists, single-statement IF/ELSE bodies)
RAISERROR ('Setting up configuration variables', 10, 1)
WITH NOWAIT;

RAISERROR (@msg, 16, 1);

IF @x = 1 SET @y = 2;
ELSE SET @y = 3;

IF @debug = 1 PRINT 'debug mode';

WHILE @i < 10 SET @i = @i + 1;

-- body with BEGIN/END must never be compacted
IF @x = 2
BEGIN
    SET @y = 4;
    SET @z = 5;
END;

-- multi-line body stays broken out even with the option on
IF @x = 3
    SELECT col1
        ,col2
        ,col3
        ,col4
    FROM SomeTable
    WHERE col1 = 1;
