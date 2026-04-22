SELECT DISTINCT
             PKObject = cco.object_id
         FROM
             sys.key_constraints cco
             JOIN sys.index_columns cc ON cco.parent_object_id = cc.object_id AND cco.unique_index_id = cc.index_id
             JOIN sys.indexes as i ON cc.object_id = i.object_id AND cc.index_id = i.index_id
         WHERE
             OBJECT_NAME(parent_object_id) = @TableName AND       
             i.type = 2 AND
             is_primary_key = 0 AND
             is_unique_constraint = 1 
