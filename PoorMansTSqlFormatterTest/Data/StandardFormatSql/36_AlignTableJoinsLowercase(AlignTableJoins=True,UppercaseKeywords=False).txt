-- Test: alignTableJoins with UppercaseKeywords=false
select e.EmployeeID
    ,e.FirstName
    ,d.DeptName
from Employees          as e
inner join Departments  as d    on  e.DeptID    = d.DeptID
left join Managers      as m    on  e.ManagerID = m.ManagerID

select o.OrderID
    ,c.CompanyName
from Orders                 as o
left outer join Customers   as c    on  o.CustomerID    = c.CustomerID
