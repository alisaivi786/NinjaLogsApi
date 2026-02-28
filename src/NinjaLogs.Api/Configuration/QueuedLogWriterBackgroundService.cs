using Microsoft.Extensions.Hosting;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using System.Text.Json;

namespace NinjaLogs.Api.Configuration;

public sealed class QueuedLogWriterBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogIngestionQueue queue,
    StorageOptions storageOptions,
    StorageRuntimeMetrics metrics,
    ILogger<QueuedLogWriterBackgroundService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogIngestionQueue _queue = queue;
    private readonly StorageOptions _storageOptions = storageOptions;
    private readonly StorageRuntimeMetrics _metrics = metrics;
    private readonly ILogger<QueuedLogWriterBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IngestionPipelineOptions pipeline = _storageOptions.IngestionPipeline ?? new();
        int batchSize = Math.Max(1, pipeline.BatchSize);
        int maxRetries = Math.Max(1, pipeline.MaxWriteRetries);
        int retryDelayMs = Math.Max(10, pipeline.RetryDelayMs);

        string deadLetterRoot = Path.GetFullPath(Path.Combine(_storageOptions.LogsDirectory, pipeline.DeadLetterDirectory));
        Directory.CreateDirectory(deadLetterRoot);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                LogEvent first = await _queue.DequeueAsync(stoppingToken);
                List<LogEvent> batch = [first];
                while (batch.Count < batchSize && _queue.TryDequeue(out LogEvent? next) && next is not null)
                {
                    batch.Add(next);
                }

                using IServiceScope scope = _scopeFactory.CreateScope();
                ILogStorage storage = scope.ServiceProvider.GetRequiredService<ILogStorage>();

                foreach (LogEvent logEvent in batch)
                {
                    bool succeeded = false;
                    DateTime started = DateTime.UtcNow;
                    Exception? lastException = null;

                    for (int attempt = 1; attempt <= maxRetries; attempt++)
                    {
                        try
                        {
                            await storage.AppendAsync(logEvent, stoppingToken);
                            succeeded = true;
                            break;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            if (attempt < maxRetries)
                            {
                                await Task.Delay(retryDelayMs, stoppingToken);
                            }
                        }
                    }

                    if (succeeded)
                    {
                        _metrics.MarkWrite(DateTime.UtcNow - started, success: true);
                        await _queue.AcknowledgeAsync(stoppingToken);
                        continue;
                    }

                    _metrics.MarkWrite(TimeSpan.Zero, success: false);
                    _logger.LogError(lastException, "Failed to persist queued log event after retries.");
                    await WriteDeadLetterAsync(deadLetterRoot, logEvent, stoppingToken);
                    await _queue.AcknowledgeAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in queued log writer.");
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
            }
        }
    }

    private static async Task WriteDeadLetterAsync(string deadLetterRoot, LogEvent logEvent, CancellationToken cancellationToken)
    {
        string file = Path.Combine(deadLetterRoot, $"{DateTime.UtcNow:yyyy-MM-dd}.ndjson");
        string json = JsonSerializer.Serialize(logEvent);
        await File.AppendAllTextAsync(file, json + Environment.NewLine, cancellationToken);
    }
}
