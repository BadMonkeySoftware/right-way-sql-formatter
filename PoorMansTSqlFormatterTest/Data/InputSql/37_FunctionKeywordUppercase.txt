-- Test: FUNCTION_KEYWORD tokens respect UppercaseKeywords setting
SELECT CAST(e.Salary AS DECIMAL(10,2)), CONVERT(VARCHAR(50), e.HireDate), ISNULL(e.ManagerID, 0), DB_NAME(), COALESCE(e.Notes, 'N/A'), NULLIF(e.Status, 'Inactive'), OBJECT_ID('Employees'), SCHEMA_NAME(e.SchemaID), IIF(e.Active = 1, 'Yes', 'No')
FROM Employees e
