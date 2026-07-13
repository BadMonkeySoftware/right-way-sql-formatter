#!/bin/bash
# Probes specific T-SQL constructs against the formatter CLI and prints
# PASS/FAIL per construct. Used to pinpoint parser gaps found by the
# real-world corpus sweep.

set -u
cd "$(dirname "$0")/.."
FMT="RightWaySqlFormatter.CmdLine/bin/Release/net10.0/SqlFormatter"
[ -x "$FMT" ] || { echo "Build first: dotnet build -c Release RightWaySqlFormatter.CmdLine/RightWaySqlFormatter.CmdLine.csproj"; exit 1; }

probe() { # name, sql via stdin
    local name="$1"
    local sql; sql="$(cat)"
    local out; out="$(printf '%s' "$sql" | "$FMT" 2>/dev/null)"; local code=$?
    if [ $code -eq 0 ]; then
        echo "PASS  $name"
    else
        echo "FAIL($code)  $name"
        printf '%s\n' "$out" | grep '^--   ' | sed 's/^--  /       /'
    fi
}

probe "proc with parenthesized params + WITH RECOMPILE" <<'SQL'
CREATE PROCEDURE dbo.p
(
    @a int = 0,
    @b varchar(5) = NULL OUTPUT
)
WITH RECOMPILE
AS
BEGIN
    SELECT x = @a;
END;
SQL

probe "proc parenthesized params, no WITH" <<'SQL'
CREATE PROCEDURE dbo.p
(
    @a int = 0
)
AS
BEGIN
    SELECT x = @a;
END;
SQL

probe "WITH RECOMPILE, unparenthesized params (baseline)" <<'SQL'
CREATE PROCEDURE dbo.p
    @a int = 0
WITH RECOMPILE
AS
BEGIN
    SELECT x = @a;
END;
SQL

probe "WITH XMLNAMESPACES + UPDATE" <<'SQL'
WITH XMLNAMESPACES ('http://x' AS p)
UPDATE b SET b.y = 1 FROM t AS b;
SQL

probe "WITH XMLNAMESPACES + regular CTE combined" <<'SQL'
WITH XMLNAMESPACES('http://x' AS p),
c AS (SELECT 1 AS n)
SELECT n FROM c;
SQL

probe "CTE inside BEGIN TRY inside WHILE" <<'SQL'
BEGIN TRY
    WHILE 1 = 1
    BEGIN
        WITH q AS (SELECT 1 AS n)
        SELECT n FROM q;
    END;
END TRY
BEGIN CATCH
    THROW;
END CATCH;
SQL

probe "THROW statement" <<'SQL'
BEGIN TRY
    SELECT 1;
END TRY
BEGIN CATCH
    THROW;
END CATCH;
SQL

probe "Service Broker: BEGIN DIALOG + SEND" <<'SQL'
DECLARE @h UNIQUEIDENTIFIER;
BEGIN DIALOG @h
    FROM SERVICE [//x/Svc]
    TO SERVICE N'//x/Svc'
    ON CONTRACT [//x/Contract]
    WITH ENCRYPTION = OFF;
SEND ON CONVERSATION @h MESSAGE TYPE [//x/Msg] (N'payload');
SQL

probe "Service Broker: WAITFOR(RECEIVE), END CONVERSATION" <<'SQL'
DECLARE @h UNIQUEIDENTIFIER, @m XML;
WAITFOR
(
    RECEIVE TOP(1) @h = conversation_handle, @m = CONVERT(XML, message_body)
    FROM dbo.Q
), TIMEOUT 1000;
END CONVERSATION @h;
SQL

probe "DROP objects: SERVICE/QUEUE/CONTRACT/MESSAGE TYPE" <<'SQL'
DROP SERVICE [//x/Svc];
DROP QUEUE dbo.Q;
DROP CONTRACT [//x/Contract];
DROP MESSAGE TYPE [//x/Msg];
SQL

probe "DROP ... IF EXISTS" <<'SQL'
DROP PROCEDURE IF EXISTS dbo.p;
DROP TABLE IF EXISTS dbo.t;
DROP SCHEMA IF EXISTS s;
SQL

probe "IF(EXISTS(...))<no space>statement" <<'SQL'
IF(EXISTS(SELECT 1 FROM sys.tables WHERE name = 'x'))DROP TABLE dbo.x;
SQL

probe "WAITFOR DELAY @variable" <<'SQL'
DECLARE @d varchar(8) = '00:00:01';
WAITFOR DELAY @d;
SQL

probe "INSERT with table hint WITH (TABLOCK)" <<'SQL'
INSERT dbo.t WITH (TABLOCK) (a, b)
SELECT 1, 2;
SQL

probe "CTE then INSERT with WITH (TABLOCK) hint" <<'SQL'
WITH c AS (SELECT 1 AS n)
INSERT #t WITH (TABLOCK) (n)
SELECT n FROM c;
SQL

probe "UPDATE with table hint" <<'SQL'
UPDATE t WITH (ROWLOCK) SET a = 1 WHERE b = 2;
SQL
