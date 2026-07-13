SELECT TOP (1000)
    [DepartmentID] AS [DepartmentID]
    ,[Name] AS [Name]
    ,[GroupName] AS [GroupName]
    ,[ModifiedDate] AS [ModifiedDate]
FROM [HumanResources].[Department]

SELECT DISTINCT
    [DepartmentID] AS [DepartmentID]
    ,[Name] AS [Name]
    ,[GroupName] AS [GroupName]
FROM [HumanResources].[Department]
