using NinjaLogs.Modules.Logging.Domain.Enums;

namespace NinjaLogs.Modules.Logging.Domain.Models;

public sealed record LogQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    LogLevel? Level = null,
    string? ServiceName = null,
    string? Environment = null,
    string? TraceId = null,
    string? CorrelationId = null,
    string? RequestId = null,
    string? RequestMethod = null,
    int? StatusCode = null,
    string? SearchText = null,
    int Page = 1,
    int PageSize = 100);
