-- Test: alignTableJoins setting
SELECT e.EmployeeID, e.FirstName, d.DeptName
FROM Employees e
INNER JOIN Departments d ON e.DeptID = d.DeptID
LEFT JOIN Managers m ON e.ManagerID = m.ManagerID

SELECT o.OrderID, c.CompanyName
FROM Orders o
LEFT OUTER JOIN Customers c ON o.CustomerID = c.CustomerID
