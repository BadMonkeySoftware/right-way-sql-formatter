-- Test: RemoveHarmlessBrackets option (upstream #133; slug RmBrackets).
-- Harmless brackets go; reserved/keyword-list, invalid-identifier and
-- token-merge-risk brackets stay. Default run leaves everything alone.
SELECT [Name], [t].[Col_1], [_x]
FROM [dbo].[MyTable] [t]
WHERE [t].[Col_1] = 1

SELECT [Order], [User], [From], [Some], [definition]
FROM [dbo].[Widgets]

SELECT [Some Name], [2Col], [a-b], [a]]b], [@v], [#tmp]
FROM [Weird Names]

SELECT *
FROM table_[some_id]
