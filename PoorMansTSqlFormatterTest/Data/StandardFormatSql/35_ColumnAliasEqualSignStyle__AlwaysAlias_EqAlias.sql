-- Test: columnAlwaysHasAlias with EqualSign style
SELECT EmployeeID = e.EmployeeID
    ,FirstName = e.FirstName
    ,ColumnAlias_1 = UPPER(e.LastName)
    ,Hired = e.HireDate
FROM Employees e
