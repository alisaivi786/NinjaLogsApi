using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using System.Text.Json;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.File;

public sealed class FileLogStorage : ILogStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _logsDirectory;

    public FileLogStorage(StorageOptions options)
    {
        string configuredPath = options.LogsDirectory;
        _logsDirectory = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath) ? "logs" : configuredPath);
        Directory.CreateDirectory(_logsDirectory);
    }

    public Task AppendAsync(LogEvent log, CancellationToken cancellationToken = default)
    {
        DateTime utcTimestamp = DateTime.SpecifyKind(log.TimestampUtc, DateTimeKind.Utc);
        string filePath = GetFilePathForDate(utcTimestamp.Date);
        string jsonLine = JsonSerializer.Serialize(log with { TimestampUtc = utcTimestamp }, JsonOptions) + Environment.NewLine;
        return System.IO.File.AppendAllTextAsync(filePath, jsonLine, cancellationToken);
    }

    public async Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 100 : Math.Min(query.PageSize, 500);

        List<LogEvent> events = [];
        foreach (string filePath in ResolveFiles(query.FromUtc, query.ToUtc))
        {
            if (!System.IO.File.Exists(filePath))
            {
                continue;
            }

            await foreach (string? line in ReadLinesAsync(filePath, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                LogEvent? parsed = null;
                try
                {
                    parsed = JsonSerializer.Deserialize<LogEvent>(line, JsonOptions);
                }
                catch (JsonException)
                {
                }

                if (parsed is null || !Matches(parsed, query))
                {
                    continue;
                }

                events.Add(parsed);
            }
        }

        List<LogEvent> ordered = events
            .OrderByDescending(x => x.TimestampUtc)
            .ToList();

        int total = ordered.Count;
        List<LogEvent> pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<LogEvent>(pageItems, total, page, pageSize);
    }

    private string GetFilePathForDate(DateTime utcDate) =>
        Path.Combine(_logsDirectory, $"{utcDate:yyyy-MM-dd}.ndjson");

    private IEnumerable<string> ResolveFiles(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue || toUtc.HasValue)
        {
            DateTime fromDate = (fromUtc ?? DateTime.UtcNow.Date).Date;
            DateTime toDate = (toUtc ?? DateTime.UtcNow.Date).Date;
            if (toDate < fromDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            for (DateTime date = fromDate; date <= toDate; date = date.AddDays(1))
            {
                yield return GetFilePathForDate(date);
            }

            yield break;
        }

        foreach (string path in Directory.EnumerateFiles(_logsDirectory, "*.ndjson"))
        {
            yield return path;
        }
    }

    private static bool Matches(LogEvent log, LogQuery query)
    {
        if (query.FromUtc.HasValue && log.TimestampUtc < query.FromUtc.Value)
        {
            return false;
        }

        if (query.ToUtc.HasValue && log.TimestampUtc > query.ToUtc.Value)
        {
            return false;
        }

        if (query.Level.HasValue && log.Level != query.Level.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.ServiceName) &&
            !string.Equals(log.ServiceName, query.ServiceName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.Environment) &&
            !string.Equals(log.Environment, query.Environment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.TraceId) &&
            !string.Equals(log.TraceId, query.TraceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId) &&
            !string.Equals(log.CorrelationId, query.CorrelationId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.RequestId) &&
            !string.Equals(log.RequestId, query.RequestId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.RequestMethod) &&
            !string.Equals(log.RequestMethod, query.RequestMethod, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.StatusCode.HasValue && log.StatusCode != query.StatusCode.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            string search = query.SearchText.Trim();
            bool hit =
                Contains(log.Message, search) ||
                Contains(log.Exception, search) ||
                Contains(log.PropertiesJson, search) ||
                Contains(log.ServiceName, search) ||
                Contains(log.Environment, search) ||
                Contains(log.TraceId, search) ||
                Contains(log.CorrelationId, search) ||
                Contains(log.RequestId, search) ||
                Contains(log.RequestPath, search) ||
                Contains(log.RequestMethod, search) ||
                Contains(log.RequestHeadersJson, search) ||
                Contains(log.ResponseHeadersJson, search) ||
                Contains(log.UserId, search) ||
                Contains(log.UserName, search) ||
                Contains(log.SourceContext, search);
            if (!hit)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string? source, string search) =>
        source?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static async IAsyncEnumerable<string?> ReadLinesAsync(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using StreamReader reader = System.IO.File.OpenText(filePath);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await reader.ReadLineAsync(cancellationToken);
        }
    }
}
