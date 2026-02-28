using System.Threading.Channels;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Services;

public sealed class BoundedLogIngestionQueue(int capacity = 20_000) : ILogIngestionQueue
{
    private long _nextSequence;
    private readonly Channel<IngestionQueueItem> _channel = Channel.CreateBounded<IngestionQueueItem>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false
    });

    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;

    public ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        IngestionQueueItem item = new(
            Interlocked.Increment(ref _nextSequence),
            DateTime.UtcNow,
            logEvent);
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public ValueTask<IngestionQueueItem> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public bool TryDequeue(out IngestionQueueItem? item)
    {
        return _channel.Reader.TryRead(out item);
    }

    public ValueTask AcknowledgeAsync(long sequence, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
