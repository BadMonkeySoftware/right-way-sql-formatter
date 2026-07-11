-- Test for AlignColumnDefinitions combined with ColumnAliasStyle=EqualSign
SELECT c.CustomerID AS ID
    ,[Hire Date] AS [Hire Date]
    ,c.[Customer Name] AS [Customer Name]
    ,[Active Flag] = c.IsActive
    ,CASE 
        WHEN Active = 1
            THEN 'Yes'
        ELSE 'No'
        END AS Flagg2
FROM Customers c
WHERE c.IsActive = 1
