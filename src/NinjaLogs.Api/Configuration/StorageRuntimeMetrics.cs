namespace NinjaLogs.Api.Configuration;

public sealed class StorageRuntimeMetrics
{
    private static readonly TimeSpan UptimeResolutionFloor = TimeSpan.FromSeconds(1);
    private const int LatencyWindowSize = 20_000;
    private readonly object _latencyLock = new();
    private readonly Queue<long> _ingestionLatencyMs = new();
    private readonly Queue<long> _dbInsertDurationMs = new();
    private readonly DateTime _startedUtc = DateTime.UtcNow;
    private long _ingestedCount;
    private long _writtenCount;
    private long _failedWrites;
    private long _queryCount;
    private long _writeLatencyMsTotal;
    private long _queryLatencyMsTotal;

    public void MarkQueued() => Interlocked.Increment(ref _ingestedCount);

    public void MarkWrite(DateTime enqueuedUtc, TimeSpan dbInsertElapsed, bool success)
    {
        if (success)
        {
            Interlocked.Increment(ref _writtenCount);
            long dbMs = (long)Math.Max(1, dbInsertElapsed.TotalMilliseconds);
            Interlocked.Add(ref _writeLatencyMsTotal, dbMs);
            long ingestionMs = (long)Math.Max(1, (DateTime.UtcNow - enqueuedUtc).TotalMilliseconds);
            AddWindowSample(_dbInsertDurationMs, dbMs);
            AddWindowSample(_ingestionLatencyMs, ingestionMs);
            return;
        }

        Interlocked.Increment(ref _failedWrites);
    }

    public void MarkQuery(TimeSpan elapsed)
    {
        Interlocked.Increment(ref _queryCount);
        Interlocked.Add(ref _queryLatencyMsTotal, (long)Math.Max(1, elapsed.TotalMilliseconds));
    }

    public RuntimeMetricsSnapshot Snapshot()
    {
        long ingested = Interlocked.Read(ref _ingestedCount);
        long written = Interlocked.Read(ref _writtenCount);
        long failed = Interlocked.Read(ref _failedWrites);
        long queryCount = Interlocked.Read(ref _queryCount);
        long writeTotal = Interlocked.Read(ref _writeLatencyMsTotal);
        long queryTotal = Interlocked.Read(ref _queryLatencyMsTotal);

        TimeSpan uptime = DateTime.UtcNow - _startedUtc;
        double seconds = Math.Max(UptimeResolutionFloor.TotalSeconds, uptime.TotalSeconds);
        return new RuntimeMetricsSnapshot(
            ingested,
            written,
            failed,
            queryCount,
            written / seconds,
            written == 0 ? 0 : writeTotal / (double)written,
            queryCount == 0 ? 0 : queryTotal / (double)queryCount,
            GetP95(_ingestionLatencyMs),
            GetP95(_dbInsertDurationMs));
    }

    private void AddWindowSample(Queue<long> queue, long value)
    {
        lock (_latencyLock)
        {
            queue.Enqueue(value);
            while (queue.Count > LatencyWindowSize)
            {
                queue.Dequeue();
            }
        }
    }

    private double GetP95(Queue<long> queue)
    {
        lock (_latencyLock)
        {
            if (queue.Count == 0)
            {
                return 0;
            }

            long[] copy = queue.ToArray();
            Array.Sort(copy);
            int index = (int)Math.Ceiling(copy.Length * 0.95) - 1;
            index = Math.Clamp(index, 0, copy.Length - 1);
            return copy[index];
        }
    }
}

public sealed record RuntimeMetricsSnapshot(
    long QueuedCount,
    long WrittenCount,
    long FailedWriteCount,
    long QueryCount,
    double WrittenPerSecond,
    double AvgWriteLatencyMs,
    double AvgQueryLatencyMs,
    double P95IngestionLatencyMs,
    double P95DbInsertDurationMs);
