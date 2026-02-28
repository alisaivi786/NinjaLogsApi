using NinjaLogs.Modules.Logging.Domain.Enums;

namespace NinjaLogs.Modules.Logging.Domain.Entities;

public sealed record LogEvent(
    DateTime TimestampUtc,
    LogLevel Level,
    string Message,
    string? ServiceName,
    string? Environment,
    string? Exception,
    string? PropertiesJson,
    string? EventId = null,
    string? SourceContext = null,
    string? RequestId = null,
    string? CorrelationId = null,
    string? TraceId = null,
    string? SpanId = null,
    string? UserId = null,
    string? UserName = null,
    string? ClientIp = null,
    string? UserAgent = null,
    string? MachineName = null,
    string? Application = null,
    string? Version = null,
    string? RequestPath = null,
    string? RequestMethod = null,
    int? StatusCode = null,
    double? DurationMs = null,
    string? RequestHeadersJson = null,
    string? ResponseHeadersJson = null,
    string? RequestBody = null,
    string? ResponseBody = null);
