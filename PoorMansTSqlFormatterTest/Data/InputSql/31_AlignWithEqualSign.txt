-- Test for AlignColumnDefinitions combined with ColumnAliasStyle=EqualSign
SELECT 
c.CustomerID AS ID, [Hire Date], c.[Customer Name] AS [Customer Name], [Active Flag] = c.IsActive,
case WHEN Active = 1 then 'Yes' else 'No' end as Flagg2
FROM Customers c
WHERE c.IsActive = 1
