-- Test: columnAlwaysHasAlias with EqualSign style
SELECT e.EmployeeID, e.FirstName, UPPER(e.LastName), e.HireDate AS Hired
FROM Employees e
