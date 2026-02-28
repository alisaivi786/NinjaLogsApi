namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer.Persistence;

public static class SqlServerSchema
{
    public const string CreateLogsTableSql = """
        IF OBJECT_ID('dbo.Logs', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Logs (
                Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                TimestampUtc DATETIME2 NOT NULL,
                Level INT NOT NULL,
                ServiceName NVARCHAR(200) NULL,
                Environment NVARCHAR(100) NULL,
                Message NVARCHAR(MAX) NOT NULL,
                Exception NVARCHAR(MAX) NULL,
                PropertiesJson NVARCHAR(MAX) NULL,
                EventId NVARCHAR(200) NULL,
                SourceContext NVARCHAR(400) NULL,
                RequestId NVARCHAR(200) NULL,
                CorrelationId NVARCHAR(200) NULL,
                TraceId NVARCHAR(200) NULL,
                SpanId NVARCHAR(200) NULL,
                UserId NVARCHAR(200) NULL,
                UserName NVARCHAR(300) NULL,
                ClientIp NVARCHAR(100) NULL,
                UserAgent NVARCHAR(1000) NULL,
                MachineName NVARCHAR(200) NULL,
                Application NVARCHAR(300) NULL,
                Version NVARCHAR(100) NULL,
                RequestPath NVARCHAR(500) NULL,
                RequestMethod NVARCHAR(20) NULL,
                StatusCode INT NULL,
                DurationMs FLOAT NULL,
                RequestHeadersJson NVARCHAR(MAX) NULL,
                ResponseHeadersJson NVARCHAR(MAX) NULL,
                RequestBody NVARCHAR(MAX) NULL,
                ResponseBody NVARCHAR(MAX) NULL
            );
        END
        """;

    public const string CreateIndexesSql = """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_TimestampUtc_Id' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_TimestampUtc_Id ON dbo.Logs (TimestampUtc DESC, Id DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_Level' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_Level ON dbo.Logs (Level);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_ServiceName' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_ServiceName ON dbo.Logs (ServiceName);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_TraceId' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_TraceId ON dbo.Logs (TraceId);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_CorrelationId' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_CorrelationId ON dbo.Logs (CorrelationId);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_RequestId' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_RequestId ON dbo.Logs (RequestId);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_StatusCode' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_StatusCode ON dbo.Logs (StatusCode);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_Level_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_Level_TimestampUtc ON dbo.Logs (Level, TimestampUtc DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_ServiceName_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_ServiceName_TimestampUtc ON dbo.Logs (ServiceName, TimestampUtc DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_Environment_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_Environment_TimestampUtc ON dbo.Logs (Environment, TimestampUtc DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_TraceId_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_TraceId_TimestampUtc ON dbo.Logs (TraceId, TimestampUtc DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_CorrelationId_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_CorrelationId_TimestampUtc ON dbo.Logs (CorrelationId, TimestampUtc DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_RequestId_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_RequestId_TimestampUtc ON dbo.Logs (RequestId, TimestampUtc DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_RequestMethod_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_RequestMethod_TimestampUtc ON dbo.Logs (RequestMethod, TimestampUtc DESC);

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_StatusCode_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_StatusCode_TimestampUtc ON dbo.Logs (StatusCode, TimestampUtc DESC);
        """;
}
