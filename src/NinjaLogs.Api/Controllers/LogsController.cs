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
    IngestionApiKeyValidator apiKeyValidator,
    StorageQuotaService quotaService,
    StorageRuntimeMetrics metrics,
    IngestionProtectionService protectionService,
    IngestionQuotaCoordinator quotaCoordinator,
    LogDataSanitizer sanitizer) : BaseApiController
{
    private readonly ILogIngestionService _ingestionService = ingestionService;
    private readonly ILogQueryService _queryService = queryService;
    private readonly IngestionApiKeyValidator _apiKeyValidator = apiKeyValidator;
    private readonly StorageQuotaService _quotaService = quotaService;
    private readonly StorageRuntimeMetrics _metrics = metrics;
    private readonly IngestionProtectionService _protectionService = protectionService;
    private readonly IngestionQuotaCoordinator _quotaCoordinator = quotaCoordinator;
    private readonly LogDataSanitizer _sanitizer = sanitizer;

    [HttpPost]
    public async Task<IActionResult> PostAsync([FromBody] CreateLogRequest request, CancellationToken cancellationToken)
    {
        string? apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!_apiKeyValidator.IsValid(apiKey))
        {
            return Unauthorized(new { message = "Invalid or missing ingestion API key." });
        }

        if (!_protectionService.IsPayloadAllowed(Request.ContentLength, out long maxPayloadBytes))
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new
            {
                message = "Payload too large.",
                maxPayloadBytes
            });
        }

        if (!_protectionService.TryConsume(apiKey!, out int retryAfterSeconds))
        {
            Response.Headers.RetryAfter = retryAfterSeconds.ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Rate limit exceeded for API key.",
                retryAfterSeconds
            });
        }

        using IDisposable gate = await _quotaCoordinator.EnterAsync(cancellationToken);
        var quota = await _quotaService.CheckAsync(cancellationToken);
        if (!quota.Allowed)
        {
            return StatusCode(quota.StatusCode, new
            {
                message = "Log ingestion quota exceeded. Upgrade license to continue ingesting logs.",
                currentBytes = quota.CurrentBytes,
                maxBytes = quota.MaxBytes
            });
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

        logEvent = _sanitizer.Sanitize(logEvent);
        await _ingestionService.IngestAsync(logEvent, cancellationToken);
        _metrics.MarkQueued();
        return Accepted();
    }

    [HttpGet]
    public async Task<PagedResult<LogEvent>> GetAsync(
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
        DateTime started = DateTime.UtcNow;
        PagedResult<LogEvent> result = await _queryService.QueryAsync(query, cancellationToken);
        _metrics.MarkQuery(DateTime.UtcNow - started);
        return result;
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
