--random sample from msdn library: http://msdn.microsoft.com/en-us/library/bb630263.aspx
with paths (
    path
    ,EmployeeID
    )
as (
    -- This section provides the value for the root of the hierarchy
    select hierarchyid::GetRoot() as OrgNode
        ,EmployeeID
    from #Children as C
    where ManagerID is null
    
    union all
    
    -- This section provides values for all nodes except the root
    select cast(p.path.ToString() + cast(C.Num as varchar(30)) + '/' as hierarchyid)
        ,C.EmployeeID
    from #Children as C
    inner join paths as p on C.ManagerID = P.EmployeeID
    )
insert NewOrg (
    OrgNode
    ,O.EmployeeID
    ,O.LoginID
    ,O.ManagerID
    )
select P.path
    ,O.EmployeeID
    ,O.LoginID
    ,O.ManagerID
from EmployeeDemo as O
inner join Paths as P on O.EmployeeID = P.EmployeeID
go

--similar sample, with 2 CTEs in the same query
begin
    with FirstCTE
    as (
        select 1 as FirstColumn
        )
        ,SecondCTE (AnotherColumn)
    as (
        select 2
        )
    select *
    from FirstCTE
    
    union
    
    select *
    from SecondCTE
end
go


