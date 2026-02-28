namespace NinjaLogs.Api.Configuration;

public sealed class StorageRuntimeMetrics
{
    private long _ingestedCount;
    private long _writtenCount;
    private long _failedWrites;
    private long _queryCount;
    private long _writeLatencyMsTotal;
    private long _queryLatencyMsTotal;

    public void MarkQueued() => Interlocked.Increment(ref _ingestedCount);

    public void MarkWrite(TimeSpan elapsed, bool success)
    {
        if (success)
        {
            Interlocked.Increment(ref _writtenCount);
            Interlocked.Add(ref _writeLatencyMsTotal, (long)Math.Max(1, elapsed.TotalMilliseconds));
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

        return new RuntimeMetricsSnapshot(
            ingested,
            written,
            failed,
            queryCount,
            written == 0 ? 0 : writeTotal / (double)written,
            queryCount == 0 ? 0 : queryTotal / (double)queryCount);
    }
}

public sealed record RuntimeMetricsSnapshot(
    long QueuedCount,
    long WrittenCount,
    long FailedWriteCount,
    long QueryCount,
    double AvgWriteLatencyMs,
    double AvgQueryLatencyMs);
