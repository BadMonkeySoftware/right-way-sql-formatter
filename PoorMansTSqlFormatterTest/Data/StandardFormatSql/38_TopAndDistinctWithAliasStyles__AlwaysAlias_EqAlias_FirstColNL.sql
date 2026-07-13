SELECT TOP (1000)
    [DepartmentID] = [DepartmentID]
    ,[Name] = [Name]
    ,[GroupName] = [GroupName]
    ,[ModifiedDate] = [ModifiedDate]
FROM [HumanResources].[Department]

SELECT DISTINCT
    [DepartmentID] = [DepartmentID]
    ,[Name] = [Name]
    ,[GroupName] = [GroupName]
FROM [HumanResources].[Department]
