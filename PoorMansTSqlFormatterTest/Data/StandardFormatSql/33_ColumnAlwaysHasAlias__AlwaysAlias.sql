-- Test: columnAlwaysHasAlias setting
SELECT e.EmployeeID AS EmployeeID
    ,e.FirstName AS FirstName
    ,UPPER(e.LastName) AS ColumnAlias_1
    ,e.HireDate AS Hired
FROM Employees e

SELECT *
    ,COUNT(*) AS ColumnAlias_1
    ,e.Salary * 1.1 AS ColumnAlias_2
FROM Employees e
