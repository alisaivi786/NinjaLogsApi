using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;
using System.Text.Json;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

public sealed class SegmentQueryReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _dataDirectory;

    public SegmentQueryReader(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
    }

    public async Task<PagedResult<LogEvent>> QueryAsync(SegmentManifestState state, LogQuery query, CancellationToken cancellationToken)
    {
        int page = query.Page < 1 ? 1 : query.Page;
        int pageSize = query.PageSize < 1 ? 100 : Math.Min(query.PageSize, 500);

        List<LogEvent> all = [];
        foreach (var segment in state.Manifest.Segments.OrderByDescending(x => x.SegmentNumber))
        {
            string path = Path.Combine(_dataDirectory, segment.FileName);
            if (!System.IO.File.Exists(path))
            {
                continue;
            }

            await foreach (string? line in ReadLinesAsync(path, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                LogEvent? evt = null;
                try { evt = JsonSerializer.Deserialize<LogEvent>(line, JsonOptions); }
                catch (JsonException) { }

                if (evt is not null && SegmentLogMatcher.Matches(evt, query))
                {
                    all.Add(evt);
                }
            }
        }

        List<LogEvent> ordered = all.OrderByDescending(x => x.TimestampUtc).ToList();
        int total = ordered.Count;
        List<LogEvent> items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<LogEvent>(items, total, page, pageSize);
    }

    private static async IAsyncEnumerable<string?> ReadLinesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using StreamReader reader = System.IO.File.OpenText(path);
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return await reader.ReadLineAsync(cancellationToken);
        }
    }
}
