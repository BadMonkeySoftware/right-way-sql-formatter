-- Test: columnAlwaysHasAlias setting
SELECT e.EmployeeID, e.FirstName, UPPER(e.LastName), e.HireDate AS Hired
FROM Employees e

SELECT *, COUNT(*), e.Salary * 1.1
FROM Employees e
