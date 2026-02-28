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
        int workers = Math.Clamp(pipeline.WriterWorkers, 1, 2);
        int maxRetries = Math.Max(1, pipeline.MaxWriteRetries);
        int retryDelayMs = Math.Max(10, pipeline.RetryDelayMs);

        string deadLetterRoot = Path.GetFullPath(Path.Combine(_storageOptions.LogsDirectory, pipeline.DeadLetterDirectory));
        Directory.CreateDirectory(deadLetterRoot);

        Task[] tasks = Enumerable.Range(0, workers)
            .Select(_ => RunWriterWorkerAsync(batchSize, maxRetries, retryDelayMs, deadLetterRoot, stoppingToken))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task RunWriterWorkerAsync(int batchSize, int maxRetries, int retryDelayMs, string deadLetterRoot, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                IngestionQueueItem first = await _queue.DequeueAsync(stoppingToken);
                List<IngestionQueueItem> batch = [first];
                while (batch.Count < batchSize && _queue.TryDequeue(out IngestionQueueItem? next) && next is not null)
                {
                    batch.Add(next);
                }

                using IServiceScope scope = _scopeFactory.CreateScope();
                ILogStorage storage = scope.ServiceProvider.GetRequiredService<ILogStorage>();

                bool batchSucceeded = false;
                Exception? lastException = null;
                DateTime dbStarted = DateTime.UtcNow;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        if (storage is IBulkLogStorage bulk && batch.Count > 1)
                        {
                            await bulk.AppendBatchAsync(batch.Select(x => x.Event).ToList(), stoppingToken);
                        }
                        else
                        {
                            foreach (IngestionQueueItem item in batch)
                            {
                                await storage.AppendAsync(item.Event, stoppingToken);
                            }
                        }

                        batchSucceeded = true;
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

                TimeSpan dbElapsed = DateTime.UtcNow - dbStarted;
                if (batchSucceeded)
                {
                    foreach (IngestionQueueItem item in batch)
                    {
                        _metrics.MarkWrite(item.EnqueuedUtc, dbElapsed, success: true);
                        await _queue.AcknowledgeAsync(item.Sequence, stoppingToken);
                    }
                    continue;
                }

                _metrics.MarkWrite(DateTime.UtcNow, TimeSpan.Zero, success: false);
                _logger.LogError(lastException, "Failed to persist queued log batch after retries.");
                foreach (IngestionQueueItem item in batch)
                {
                    await WriteDeadLetterAsync(deadLetterRoot, item.Event, stoppingToken);
                    await _queue.AcknowledgeAsync(item.Sequence, stoppingToken);
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
