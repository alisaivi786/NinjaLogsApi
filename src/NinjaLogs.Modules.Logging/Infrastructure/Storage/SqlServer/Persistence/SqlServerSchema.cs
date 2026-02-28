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
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Logs_TimestampUtc' AND object_id = OBJECT_ID('dbo.Logs'))
            CREATE INDEX IX_Logs_TimestampUtc ON dbo.Logs (TimestampUtc);

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
        """;
}