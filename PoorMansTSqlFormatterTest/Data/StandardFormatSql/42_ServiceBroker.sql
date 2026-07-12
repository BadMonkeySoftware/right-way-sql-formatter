-- Test: Service Broker DML/dialog constructs
DECLARE @h UNIQUEIDENTIFIER;
DECLARE @m XML;

BEGIN DIALOG @h
FROM SERVICE [//test/InitiatorSvc] TO SERVICE N'//test/TargetSvc' ON CONTRACT [//test/Contract]
WITH ENCRYPTION = OFF;

SEND ON CONVERSATION @h MESSAGE TYPE [//test/Msg](N'payload');

BEGIN DIALOG CONVERSATION @h
FROM SERVICE [//a/Svc] TO SERVICE N'//a/Svc' ON CONTRACT [//a/Contract];

WAITFOR (
        RECEIVE TOP (1) @h = conversation_handle
        ,@m = CONVERT(XML, message_body) FROM dbo.TargetQueue
        )
    ,TIMEOUT 1000;

END CONVERSATION @h;

BEGIN CONVERSATION TIMER (@h) TIMEOUT = 120;

SELECT verify = CASE 
        WHEN 1 = 1
            THEN 2
        ELSE 3
        END;
