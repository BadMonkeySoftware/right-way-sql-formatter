SELECT DISTINCT PKObject = cco.OBJECT_ID
FROM sys.key_constraints    AS cco
JOIN sys.index_columns      AS cc   ON  cco.parent_object_id    = cc.OBJECT_ID
                                    AND cco.unique_index_id     = cc.index_id
JOIN sys.indexes            AS i    ON  cc.OBJECT_ID            = i.OBJECT_ID
                                    AND cc.index_id             = i.index_id
WHERE OBJECT_NAME(parent_object_id) = @TableName
    AND i.type = 2
    AND is_primary_key = 0
    AND is_unique_constraint = 1
