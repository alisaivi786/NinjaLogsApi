using System.Threading.Channels;
using System.Text.Json;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Configuration;

public sealed class DurableSpoolIngestionQueue : ILogIngestionQueue
{
    private readonly Channel<QueueEnvelope> _channel;
    private readonly SemaphoreSlim _appendLock = new(1, 1);
    private readonly string _spoolFilePath;
    private readonly string _checkpointFilePath;
    private long _nextSequence;
    private long _ackedSequence;
    private readonly SortedSet<long> _acknowledged = [];
    private readonly object _ackLock = new();
    private int _ackSinceLastCompaction;

    public DurableSpoolIngestionQueue(StorageOptions storageOptions)
    {
        IngestionPipelineOptions pipeline = storageOptions.IngestionPipeline ?? new();
        int capacity = Math.Max(100, pipeline.QueueCapacity);

        _channel = Channel.CreateBounded<QueueEnvelope>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        string queueDirectory = Path.GetFullPath(Path.Combine(storageOptions.LogsDirectory, "queue"));
        Directory.CreateDirectory(queueDirectory);
        _spoolFilePath = Path.Combine(queueDirectory, "spool.ndjson");
        _checkpointFilePath = Path.Combine(queueDirectory, "checkpoint.txt");

        LoadCheckpoint();

        LoadExistingEventsIntoMemory();
    }

    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;

    public async ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        long seq = Interlocked.Increment(ref _nextSequence);
        QueueEnvelope envelope = new(seq, DateTime.UtcNow, logEvent);
        string line = JsonSerializer.Serialize(envelope) + Environment.NewLine;

        await _appendLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_spoolFilePath, line, cancellationToken);
        }
        finally
        {
            _appendLock.Release();
        }

        await _channel.Writer.WriteAsync(envelope, cancellationToken);
    }

    public async ValueTask<IngestionQueueItem> DequeueAsync(CancellationToken cancellationToken = default)
    {
        QueueEnvelope envelope = await _channel.Reader.ReadAsync(cancellationToken);
        DateTime enqueuedUtc = envelope.EnqueuedUtc == default ? DateTime.UtcNow : envelope.EnqueuedUtc;
        return new IngestionQueueItem(envelope.Seq, enqueuedUtc, envelope.Event);
    }

    public bool TryDequeue(out IngestionQueueItem? item)
    {
        if (!_channel.Reader.TryRead(out QueueEnvelope? envelope) || envelope is null)
        {
            item = null;
            return false;
        }

        DateTime enqueuedUtc = envelope.EnqueuedUtc == default ? DateTime.UtcNow : envelope.EnqueuedUtc;
        item = new IngestionQueueItem(envelope.Seq, enqueuedUtc, envelope.Event);
        return true;
    }

    public async ValueTask AcknowledgeAsync(long sequence, CancellationToken cancellationToken = default)
    {
        if (sequence <= 0)
        {
            return;
        }

        bool shouldCompact = false;
        long highestContiguousAck;
        lock (_ackLock)
        {
            if (sequence <= _ackedSequence)
            {
                return;
            }

            _acknowledged.Add(sequence);
            while (_acknowledged.Remove(_ackedSequence + 1))
            {
                _ackedSequence++;
                _ackSinceLastCompaction++;
            }

            highestContiguousAck = _ackedSequence;
            if (_ackSinceLastCompaction >= 500)
            {
                _ackSinceLastCompaction = 0;
                shouldCompact = true;
            }
        }

        await File.WriteAllTextAsync(_checkpointFilePath, highestContiguousAck.ToString(), cancellationToken);
        if (shouldCompact)
        {
            await CompactAsync(cancellationToken);
        }
    }

    private void LoadExistingEventsIntoMemory()
    {
        if (!File.Exists(_spoolFilePath))
        {
            return;
        }

        foreach (string line in File.ReadLines(_spoolFilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                QueueEnvelope? envelope = JsonSerializer.Deserialize<QueueEnvelope>(line);
                if (envelope?.Event is null)
                {
                    continue;
                }

                _nextSequence = Math.Max(_nextSequence, envelope.Seq);
                if (envelope.Seq > _ackedSequence)
                {
                    _channel.Writer.TryWrite(envelope);
                }
            }
            catch (JsonException)
            {
                // Ignore malformed lines to keep startup robust.
            }
        }
    }

    private void LoadCheckpoint()
    {
        if (!File.Exists(_checkpointFilePath))
        {
            return;
        }

        string text = File.ReadAllText(_checkpointFilePath).Trim();
        if (long.TryParse(text, out long seq) && seq >= 0)
        {
            _ackedSequence = seq;
        }
    }

    private async Task CompactAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_spoolFilePath))
        {
            return;
        }

        string tempFile = _spoolFilePath + ".tmp";
        await using StreamWriter writer = new(tempFile, false);
        foreach (string line in File.ReadLines(_spoolFilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            QueueEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<QueueEnvelope>(line);
            }
            catch (JsonException)
            {
                continue;
            }

            if (envelope is null || envelope.Seq <= _ackedSequence)
            {
                continue;
            }

            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync();
        File.Copy(tempFile, _spoolFilePath, overwrite: true);
        File.Delete(tempFile);
    }

    private sealed record QueueEnvelope(long Seq, DateTime EnqueuedUtc, LogEvent Event);
}
