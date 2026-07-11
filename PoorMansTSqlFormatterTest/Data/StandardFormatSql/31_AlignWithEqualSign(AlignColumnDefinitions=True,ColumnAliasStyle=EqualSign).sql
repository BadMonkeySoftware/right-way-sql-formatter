-- Test for AlignColumnDefinitions combined with ColumnAliasStyle=EqualSign
SELECT ID              = c.CustomerID
    ,[Hire Date]
    ,[Customer Name] = c.[Customer Name]
    ,[Active Flag]   = c.IsActive
    ,CASE 
        WHEN Active = 1
            THEN 'Yes'
        ELSE 'No'
        END AS Flagg2
FROM Customers c
WHERE c.IsActive = 1
