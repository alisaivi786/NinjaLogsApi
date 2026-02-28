using System.Threading.Channels;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Services;

public sealed class BoundedLogIngestionQueue(int capacity = 20_000) : ILogIngestionQueue
{
    private readonly Channel<LogEvent> _channel = Channel.CreateBounded<LogEvent>(new BoundedChannelOptions(capacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;

    public ValueTask EnqueueAsync(LogEvent logEvent, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(logEvent, cancellationToken);
    }

    public ValueTask<LogEvent> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public bool TryDequeue(out LogEvent? logEvent)
    {
        return _channel.Reader.TryRead(out logEvent);
    }

    public ValueTask AcknowledgeAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
