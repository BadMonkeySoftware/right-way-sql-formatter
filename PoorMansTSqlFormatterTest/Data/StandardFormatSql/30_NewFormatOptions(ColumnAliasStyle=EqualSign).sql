-- Test file for new formatting options (v2)
SELECT EmpID = e.EmployeeID
    ,First = e.FirstName
    ,Last = e.LastName
    ,Hired = e.HireDate
FROM Employees e
INNER JOIN Departments d ON e.DeptID = d.DeptID
WHERE e.HireDate > '2020-01-01'
    AND e.IsActive = 1
    AND e.DeptID IN (
        1
        ,2
        ,3
        )

SELECT col1
    ,col2
    ,col3
FROM SomeTable
WHERE col1 = 1
    AND col2 = 2
    OR col3 = 3

CREATE TABLE dbo.Employee (
    EmployeeID INT NOT NULL
    ,FirstName NVARCHAR(50) NOT NULL
    ,LastName NVARCHAR(100) NOT NULL
    ,HireDate DATE NULL
    ,DepartmentID INT NOT NULL
    ,CONSTRAINT PK_Employee PRIMARY KEY (EmployeeID)
    ,CONSTRAINT FK_Employee_Department FOREIGN KEY (DepartmentID) REFERENCES dbo.Department(DepartmentID)
    )
