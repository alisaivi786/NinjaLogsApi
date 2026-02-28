using Microsoft.AspNetCore.Mvc;
using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;
using LogEventLevel = NinjaLogs.Modules.Logging.Domain.Enums.LogLevel;

namespace NinjaLogs.Api.Controllers;

[Route("api/v1.0/logs")]
[Route("api/logs")]
public sealed class LogsController(
    ILogIngestionService ingestionService,
    ILogQueryService queryService,
    IngestionApiKeyValidator apiKeyValidator) : BaseApiController
{
    private readonly ILogIngestionService _ingestionService = ingestionService;
    private readonly ILogQueryService _queryService = queryService;
    private readonly IngestionApiKeyValidator _apiKeyValidator = apiKeyValidator;

    [HttpPost]
    public async Task<IActionResult> PostAsync([FromBody] CreateLogRequest request, CancellationToken cancellationToken)
    {
        if (!_apiKeyValidator.IsValid(Request.Headers["X-Api-Key"].FirstOrDefault()))
        {
            return Unauthorized(new { message = "Invalid or missing ingestion API key." });
        }

        LogEvent logEvent = new(
            request.TimestampUtc ?? DateTime.UtcNow,
            request.Level,
            request.Message,
            request.ServiceName,
            request.Environment,
            request.Exception,
            request.PropertiesJson,
            request.EventId,
            request.SourceContext,
            request.RequestId,
            request.CorrelationId,
            request.TraceId,
            request.SpanId,
            request.UserId,
            request.UserName,
            request.ClientIp,
            request.UserAgent,
            request.MachineName,
            request.Application,
            request.Version,
            request.RequestPath,
            request.RequestMethod,
            request.StatusCode,
            request.DurationMs,
            request.RequestHeadersJson,
            request.ResponseHeadersJson,
            request.RequestBody,
            request.ResponseBody);

        await _ingestionService.IngestAsync(logEvent, cancellationToken);
        return Accepted();
    }

    [HttpGet]
    public Task<PagedResult<LogEvent>> GetAsync(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] LogEventLevel? level,
        [FromQuery] string? serviceName,
        [FromQuery] string? environment,
        [FromQuery] string? traceId,
        [FromQuery] string? correlationId,
        [FromQuery] string? requestId,
        [FromQuery] string? requestMethod,
        [FromQuery] int? statusCode,
        [FromQuery] string? searchText,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        LogQuery query = new(fromUtc, toUtc, level, serviceName, environment, traceId, correlationId, requestId, requestMethod, statusCode, searchText, page, pageSize);
        return _queryService.QueryAsync(query, cancellationToken);
    }
}

public sealed record CreateLogRequest(
    DateTime? TimestampUtc,
    LogEventLevel Level,
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
