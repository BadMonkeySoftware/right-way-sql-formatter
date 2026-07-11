-- Test: EqualSign-style column aliases in input (alias = expression) are preserved
SELECT FullName = FirstName + ' ' + LastName
    ,Age = 30
    ,[Start Date] = e.HireDate
    ,EmployeeID
    ,DeptName = d.Name
FROM Employees e
INNER JOIN Departments d ON e.DeptID = d.DeptID
WHERE e.IsActive = 1

SELECT Mixed1 = a.Col1
    ,Mixed2 = a.Col2
    ,a.Col3
    ,[Odd Name] = a.Col4
FROM SomeTable a

SELECT @Total = COUNT(*)
    ,@MaxDate = MAX(OrderDate)
FROM Orders

SELECT OrderID = o.ID
    ,LineTotal = o.Qty * o.Price
    ,Sub = (
        SELECT COUNT(*)
        FROM OrderLines ol
        WHERE ol.OrderID = o.ID
        )
    ,Flag = CASE 
        WHEN o.Qty > 10
            THEN 'Big'
        ELSE 'Small'
        END
FROM Orders o
