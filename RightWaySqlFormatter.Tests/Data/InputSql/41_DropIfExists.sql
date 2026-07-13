-- Test: DROP <object> IF EXISTS (SQL 2016+) plus control-flow IF EXISTS regression cases
DROP TABLE IF EXISTS dbo.SomeTable;
DROP PROCEDURE IF EXISTS dbo.SomeProc;
DROP VIEW IF EXISTS dbo.SomeView;
DROP INDEX IF EXISTS IX_Test ON dbo.SomeTable;
DROP SCHEMA IF EXISTS SomeSchema;
DROP TABLE IF EXISTS #temp1, dbo.Table2;

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'x')
BEGIN
    SELECT FoundIt = 1;
END;

IF OBJECT_ID('dbo.OldWay') IS NOT NULL
    DROP TABLE dbo.OldWay;
