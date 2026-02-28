using System.Collections.Concurrent;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Configuration;

public sealed class IngestionProtectionService(StorageOptions storageOptions)
{
    private readonly IngestionPipelineOptions _options = storageOptions.IngestionPipeline ?? new();
    private readonly ConcurrentDictionary<string, ApiKeyWindow> _windows = new(StringComparer.Ordinal);

    public bool IsPayloadAllowed(long? contentLength, out long maxBytes)
    {
        maxBytes = _options.MaxPayloadBytes;
        return !contentLength.HasValue || contentLength.Value <= maxBytes;
    }

    public bool TryConsume(string apiKey, out int retryAfterSeconds)
    {
        retryAfterSeconds = 0;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        int limit = Math.Max(1, _options.MaxRequestsPerMinutePerApiKey);
        DateTime utcNow = DateTime.UtcNow;
        DateTime windowStart = new(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0, DateTimeKind.Utc);

        ApiKeyWindow updated = _windows.AddOrUpdate(apiKey.Trim(),
            _ => new ApiKeyWindow(windowStart, 1),
            (_, existing) =>
            {
                if (existing.WindowStartUtc == windowStart)
                {
                    return existing with { Count = existing.Count + 1 };
                }

                return new ApiKeyWindow(windowStart, 1);
            });

        if (updated.Count <= limit)
        {
            return true;
        }

        retryAfterSeconds = Math.Max(1, 60 - utcNow.Second);
        return false;
    }

    private sealed record ApiKeyWindow(DateTime WindowStartUtc, int Count);
}
