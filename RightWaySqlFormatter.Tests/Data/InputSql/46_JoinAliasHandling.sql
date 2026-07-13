-- Test: AlignTableJoins on tables WITHOUT aliases (AlignTableJoinsAddAliases on/off)
SELECT Employees.EmployeeID, Departments.DeptName, m.LastName
FROM Employees
INNER JOIN Departments ON Employees.DeptID = Departments.DeptID
LEFT JOIN Managers m ON Employees.ManagerID = m.ManagerID
WHERE Employees.Active = 1
