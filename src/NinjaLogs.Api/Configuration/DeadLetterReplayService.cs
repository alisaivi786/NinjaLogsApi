using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using System.Text.Json;

namespace NinjaLogs.Api.Configuration;

public sealed class DeadLetterReplayService(StorageOptions storageOptions, ILogIngestionService ingestionService)
{
    private readonly StorageOptions _storageOptions = storageOptions;
    private readonly ILogIngestionService _ingestionService = ingestionService;

    public IReadOnlyCollection<string> ListFiles()
    {
        string root = GetDlqRoot();
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.EnumerateFiles(root, "*.ndjson", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .OrderByDescending(x => x)
            .ToArray();
    }

    public async Task<int> ReplayAsync(string fileName, int maxEvents, CancellationToken cancellationToken = default)
    {
        maxEvents = Math.Clamp(maxEvents, 1, 50_000);
        string root = GetDlqRoot();
        string fullPath = Path.GetFullPath(Path.Combine(root, fileName));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
        {
            throw new FileNotFoundException("DLQ file not found.", fileName);
        }

        int replayed = 0;
        string temp = fullPath + ".tmp";
        await using StreamWriter writer = new(temp, false);
        foreach (string line in File.ReadLines(fullPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (replayed < maxEvents)
            {
                try
                {
                    LogEvent? evt = JsonSerializer.Deserialize<LogEvent>(line);
                    if (evt is not null)
                    {
                        await _ingestionService.IngestAsync(evt, cancellationToken);
                        replayed++;
                        continue;
                    }
                }
                catch
                {
                }
            }

            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync(cancellationToken);
        File.Copy(temp, fullPath, overwrite: true);
        File.Delete(temp);
        return replayed;
    }

    private string GetDlqRoot()
    {
        string dir = _storageOptions.IngestionPipeline.DeadLetterDirectory;
        return Path.GetFullPath(Path.Combine(_storageOptions.LogsDirectory, dir));
    }
}
