using Microsoft.AspNetCore.Mvc;
using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using System.Text.Json;
using LogEventLevel = NinjaLogs.Modules.Logging.Domain.Enums.LogLevel;

namespace NinjaLogs.Api.Controllers;

[Route("api/events")]
public sealed class SeqIngestionController(
    ILogIngestionService ingestionService,
    IngestionApiKeyValidator apiKeyValidator,
    StorageQuotaService quotaService,
    StorageRuntimeMetrics metrics,
    IngestionProtectionService protectionService,
    IngestionQuotaCoordinator quotaCoordinator,
    LogDataSanitizer sanitizer) : BaseApiController
{
    private readonly ILogIngestionService _ingestionService = ingestionService;
    private readonly IngestionApiKeyValidator _apiKeyValidator = apiKeyValidator;
    private readonly StorageQuotaService _quotaService = quotaService;
    private readonly StorageRuntimeMetrics _metrics = metrics;
    private readonly IngestionProtectionService _protectionService = protectionService;
    private readonly IngestionQuotaCoordinator _quotaCoordinator = quotaCoordinator;
    private readonly LogDataSanitizer _sanitizer = sanitizer;

    [HttpPost("raw")]
    [Consumes("application/vnd.serilog.clef", "text/plain", "application/json")]
    public async Task<IActionResult> IngestRawAsync(CancellationToken cancellationToken)
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

        using StreamReader reader = new(Request.Body);
        int ingested = 0;
        List<string> lines = [];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            lines.Add(line);
            LogEvent? logEvent = TryMapClefLine(line);
            if (logEvent is not null)
            {
                logEvent = _sanitizer.Sanitize(logEvent);
                await _ingestionService.IngestAsync(logEvent, cancellationToken);
                _metrics.MarkQueued();
                ingested++;
            }
        }

        if (ingested == 0 && lines.Count > 1)
        {
            string fullBody = string.Join(Environment.NewLine, lines);
            LogEvent? singleEvent = TryMapClefLine(fullBody);
            if (singleEvent is not null)
            {
                singleEvent = _sanitizer.Sanitize(singleEvent);
                await _ingestionService.IngestAsync(singleEvent, cancellationToken);
                _metrics.MarkQueued();
                ingested++;
            }
        }

        if (ingested == 0)
        {
            return BadRequest(new
            {
                message = "No valid CLEF events were parsed. Send NDJSON (one JSON event per line) or a single JSON event body."
            });
        }

        return Accepted();
    }

    private static LogEvent? TryMapClefLine(string line)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(line);
            JsonElement root = doc.RootElement;

            DateTime timestampUtc = ReadTimestampUtc(root);
            LogEventLevel level = ReadLevel(root);
            string message = ReadString(root, "@m") ?? ReadString(root, "@mt") ?? "(no message)";
            string? exception = ReadString(root, "@x");

            string? sourceContext = ReadString(root, "SourceContext");
            string? requestId = ReadString(root, "RequestId");
            string? correlationId = ReadString(root, "CorrelationId");
            string? traceId = ReadString(root, "TraceId");
            string? spanId = ReadString(root, "SpanId");
            string? requestPath = ReadString(root, "RequestPath");
            string? requestMethod = ReadString(root, "RequestMethod");
            string? userId = ReadString(root, "UserId");
            string? userName = ReadString(root, "UserName");
            string? environment = ReadString(root, "EnvironmentName") ?? ReadString(root, "Environment");
            string? serviceName = ReadString(root, "Application") ?? ReadString(root, "ServiceName");
            string? eventId = ReadString(root, "@i");
            int? statusCode = ReadInt(root, "StatusCode");
            double? durationMs = ReadDouble(root, "Elapsed");

            string propertiesJson = JsonSerializer.Serialize(root);

            return new LogEvent(
                timestampUtc,
                level,
                message,
                serviceName,
                environment,
                exception,
                propertiesJson,
                eventId,
                sourceContext,
                requestId,
                correlationId,
                traceId,
                spanId,
                userId,
                userName,
                null,
                null,
                null,
                ReadString(root, "Application"),
                null,
                requestPath,
                requestMethod,
                statusCode,
                durationMs);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTime ReadTimestampUtc(JsonElement root)
    {
        string? raw = ReadString(root, "@t");
        return DateTime.TryParse(raw, out DateTime parsed)
            ? DateTime.SpecifyKind(parsed.ToUniversalTime(), DateTimeKind.Utc)
            : DateTime.UtcNow;
    }

    private static LogEventLevel ReadLevel(JsonElement root)
    {
        string? raw = ReadString(root, "@l");
        return raw?.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Trace,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Critical,
            _ => LogEventLevel.Information
        };
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number : null;
    }

    private static double? ReadDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number) ? number : null;
    }
}
