using System.Diagnostics;
using System.Text.Json;
using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.File;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.PostgreSQL.Repositories;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SQLite.Repositories;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SqlServer.Repositories;

const int defaultEvents = 20_000;
const int defaultBatchSize = 200;

int totalEvents = GetInt("PERF_EVENTS", defaultEvents);
int batchSize = Math.Max(1, GetInt("PERF_BATCH_SIZE", defaultBatchSize));
JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
string? reportPathArg = args.FirstOrDefault();
string reportPath = reportPathArg
    ?? Environment.GetEnvironmentVariable("PERF_REPORT_PATH")
    ?? Path.Combine("tests", "perf", "reports", $"provider-performance-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");

string runId = Guid.NewGuid().ToString("N")[..12];
DateTime startedUtc = DateTime.UtcNow;

var results = new List<ProviderBenchmarkResult>();

await RunProviderAsync("File", CreateFileStorage, supportsBulk: false, results, totalEvents, batchSize);
await RunProviderAsync("SegmentedFile", CreateSegmentedStorage, supportsBulk: false, results, totalEvents, batchSize);
await RunProviderAsync("SQLite", CreateSqliteStorage, supportsBulk: false, results, totalEvents, batchSize);
await RunProviderAsync("PostgreSQL", CreatePostgresStorage, supportsBulk: true, results, totalEvents, batchSize);
await RunProviderAsync("SqlServer", CreateSqlServerStorage, supportsBulk: false, results, totalEvents, batchSize);

var report = new PerformanceReport(
    RunId: runId,
    StartedUtc: startedUtc,
    FinishedUtc: DateTime.UtcNow,
    TotalEvents: totalEvents,
    BatchSize: batchSize,
    Results: results);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
await System.IO.File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, jsonOptions));

Console.WriteLine($"Performance report written: {Path.GetFullPath(reportPath)}");
foreach (ProviderBenchmarkResult r in results)
{
    Console.WriteLine($"- {r.Provider}: {r.Status} ({r.Note ?? ""})");
    if (r.SingleInsert is not null)
    {
        Console.WriteLine($"  single: {r.SingleInsert.ThroughputLogsPerSec:F2} logs/sec, p95 op {r.SingleInsert.P95LatencyMs:F2} ms");
    }

    if (r.BulkInsert is not null)
    {
        Console.WriteLine($"  bulk:   {r.BulkInsert.ThroughputLogsPerSec:F2} logs/sec, p95 batch {r.BulkInsert.P95LatencyMs:F2} ms");
    }

    if (r.Query is not null)
    {
        Console.WriteLine($"  query:  {r.Query.ElapsedMs:F2} ms, returned {r.Query.ReturnedCount}");
    }
}

static async Task RunProviderAsync(
    string provider,
    Func<Task<StorageFactoryResult>> factory,
    bool supportsBulk,
    List<ProviderBenchmarkResult> results,
    int totalEvents,
    int batchSize)
{
    StorageFactoryResult created;
    try
    {
        created = await factory();
    }
    catch (Exception ex)
    {
        results.Add(new ProviderBenchmarkResult(provider, "error", $"Factory failed: {ex.Message}", null, null, null));
        return;
    }

    if (!created.Enabled || created.Storage is null)
    {
        results.Add(new ProviderBenchmarkResult(provider, "skipped", created.Note ?? "Not configured.", null, null, null));
        return;
    }

    await using IAsyncDisposable? disposable = created.AsyncDisposable;
    using IDisposable? syncDisposable = created.Disposable;

    ILogStorage storage = created.Storage;

    try
    {
        // Warmup to reduce one-time schema/init impact on measured loop.
        for (int i = 0; i < 100; i++)
        {
            await storage.AppendAsync(BuildLogEvent(i, 128));
        }

        BenchmarkMetric single = await MeasureSingleInsertAsync(storage, totalEvents);
        BenchmarkMetric? bulk = null;

        if (supportsBulk && storage is IBulkLogStorage bulkStorage)
        {
            bulk = await MeasureBulkInsertAsync(bulkStorage, totalEvents, batchSize);
        }

        QueryMetric query = await MeasureQueryAsync(storage);

        results.Add(new ProviderBenchmarkResult(provider, "ok", created.Note, single, bulk, query));
    }
    catch (Exception ex)
    {
        results.Add(new ProviderBenchmarkResult(provider, "error", ex.Message, null, null, null));
    }
}

static async Task<BenchmarkMetric> MeasureSingleInsertAsync(ILogStorage storage, int totalEvents)
{
    List<double> samples = new(totalEvents);
    Stopwatch totalSw = Stopwatch.StartNew();

    for (int i = 0; i < totalEvents; i++)
    {
        Stopwatch op = Stopwatch.StartNew();
        await storage.AppendAsync(BuildLogEvent(i, 300));
        op.Stop();
        samples.Add(op.Elapsed.TotalMilliseconds);
    }

    totalSw.Stop();
    return BuildMetric("single_insert", totalEvents, totalSw.Elapsed.TotalMilliseconds, samples);
}

static async Task<BenchmarkMetric> MeasureBulkInsertAsync(IBulkLogStorage bulkStorage, int totalEvents, int batchSize)
{
    List<double> samples = new(Math.Max(1, totalEvents / batchSize + 1));
    Stopwatch totalSw = Stopwatch.StartNew();

    int sent = 0;
    while (sent < totalEvents)
    {
        int take = Math.Min(batchSize, totalEvents - sent);
        List<LogEvent> batch = new(take);
        for (int i = 0; i < take; i++)
        {
            batch.Add(BuildLogEvent(sent + i, 300));
        }

        Stopwatch op = Stopwatch.StartNew();
        await bulkStorage.AppendBatchAsync(batch);
        op.Stop();

        samples.Add(op.Elapsed.TotalMilliseconds);
        sent += take;
    }

    totalSw.Stop();
    return BuildMetric("bulk_insert", totalEvents, totalSw.Elapsed.TotalMilliseconds, samples);
}

static async Task<QueryMetric> MeasureQueryAsync(ILogStorage storage)
{
    LogQuery query = new(
        FromUtc: DateTime.UtcNow.AddDays(-1),
        ToUtc: DateTime.UtcNow.AddDays(1),
        Level: LogLevel.Error,
        ServiceName: "Perf",
        Environment: "Perf",
        TraceId: null,
        CorrelationId: null,
        RequestId: null,
        RequestMethod: "POST",
        StatusCode: 500,
        SearchText: "phase4-benchmark",
        Page: 1,
        PageSize: 200);

    Stopwatch sw = Stopwatch.StartNew();
    PagedResult<LogEvent> result = await storage.QueryAsync(query);
    sw.Stop();

    return new QueryMetric(sw.Elapsed.TotalMilliseconds, result.Items.Count, result.TotalCount);
}

static BenchmarkMetric BuildMetric(string mode, int operations, double elapsedMs, List<double> samples)
{
    samples.Sort();
    double p50 = Percentile(samples, 0.50);
    double p95 = Percentile(samples, 0.95);
    double throughput = elapsedMs <= 0 ? 0 : operations / (elapsedMs / 1000d);

    return new BenchmarkMetric(mode, operations, elapsedMs, throughput, p50, p95);
}

static double Percentile(List<double> sorted, double p)
{
    if (sorted.Count == 0)
    {
        return 0;
    }

    double raw = (sorted.Count - 1) * p;
    int low = (int)Math.Floor(raw);
    int high = (int)Math.Ceiling(raw);

    if (low == high)
    {
        return sorted[low];
    }

    double weight = raw - low;
    return sorted[low] * (1 - weight) + sorted[high] * weight;
}

static LogEvent BuildLogEvent(int i, int paddingBytes)
{
    string pad = new('x', Math.Max(0, paddingBytes));
    string propertiesJson = JsonSerializer.Serialize(new { bench = "phase4", i, pad });
    return new LogEvent(
        TimestampUtc: DateTime.UtcNow,
        Level: LogLevel.Error,
        Message: $"phase4-benchmark-{i}-{pad}",
        ServiceName: "Perf",
        Environment: "Perf",
        Exception: null,
        PropertiesJson: propertiesJson,
        TraceId: $"T-{i}",
        RequestMethod: "POST",
        StatusCode: 500);
}

static async Task<StorageFactoryResult> CreateFileStorage()
{
    string root = MakeTempDir("file");
    StorageOptions options = new() { Provider = "File", LogsDirectory = root };
    return await Task.FromResult(new StorageFactoryResult(true, new FileLogStorage(options), null, null, $"path={root}"));
}

static async Task<StorageFactoryResult> CreateSegmentedStorage()
{
    string root = MakeTempDir("segmented");
    StorageOptions options = new()
    {
        Provider = "SegmentedFile",
        LogsDirectory = root,
        SegmentedFile = new SegmentedFileStorageOptions
        {
            DataDirectory = Path.Combine(root, "segments"),
            SegmentMaxBytes = 128L * 1024 * 1024,
            ManifestFileName = "manifest.json"
        }
    };

    return await Task.FromResult(new StorageFactoryResult(true, new SegmentedFileLogStorage(options), null, null, $"path={root}"));
}

static async Task<StorageFactoryResult> CreateSqliteStorage()
{
    string root = MakeTempDir("sqlite");
    StorageOptions options = new()
    {
        Provider = "SQLite",
        LogsDirectory = root,
        Connections = new StorageConnectionOptions
        {
            SQLite = $"Data Source={Path.Combine(root, "ninjalogs-perf.db")}" 
        }
    };

    SqliteLogEventRepository repo = new(options);
    return await Task.FromResult(new StorageFactoryResult(true, new SqliteLogStorage(repo), null, null, $"path={root}"));
}

static async Task<StorageFactoryResult> CreatePostgresStorage()
{
    string? cs = Environment.GetEnvironmentVariable("NINJALOGS_POSTGRES_CS");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return await Task.FromResult(new StorageFactoryResult(false, null, null, null, "Set NINJALOGS_POSTGRES_CS to include PostgreSQL benchmark."));
    }

    StorageOptions options = new()
    {
        Provider = "PostgreSQL",
        Connections = new StorageConnectionOptions { PostgreSQL = cs }
    };

    PostgresLogEventRepository repo = new(options);
    return await Task.FromResult(new StorageFactoryResult(true, new PostgresLogStorage(repo), null, null, "connection=env:NINJALOGS_POSTGRES_CS"));
}

static async Task<StorageFactoryResult> CreateSqlServerStorage()
{
    string? cs = Environment.GetEnvironmentVariable("NINJALOGS_SQLSERVER_CS");
    if (string.IsNullOrWhiteSpace(cs))
    {
        return await Task.FromResult(new StorageFactoryResult(false, null, null, null, "Set NINJALOGS_SQLSERVER_CS to include SqlServer benchmark."));
    }

    StorageOptions options = new()
    {
        Provider = "SqlServer",
        Connections = new StorageConnectionOptions { SqlServer = cs }
    };

    SqlServerLogEventRepository repo = new(options);
    return await Task.FromResult(new StorageFactoryResult(true, new SqlServerLogStorage(repo), null, null, "connection=env:NINJALOGS_SQLSERVER_CS"));
}

static string MakeTempDir(string provider)
{
    string dir = Path.Combine(Path.GetTempPath(), $"ninjalogs-perf-{provider}-{Guid.NewGuid():N}");
    Directory.CreateDirectory(dir);
    return dir;
}

static int GetInt(string env, int fallback)
{
    string? raw = Environment.GetEnvironmentVariable(env);
    return int.TryParse(raw, out int v) ? v : fallback;
}

public sealed record PerformanceReport(
    string RunId,
    DateTime StartedUtc,
    DateTime FinishedUtc,
    int TotalEvents,
    int BatchSize,
    IReadOnlyList<ProviderBenchmarkResult> Results);

public sealed record ProviderBenchmarkResult(
    string Provider,
    string Status,
    string? Note,
    BenchmarkMetric? SingleInsert,
    BenchmarkMetric? BulkInsert,
    QueryMetric? Query);

public sealed record BenchmarkMetric(
    string Mode,
    int Operations,
    double ElapsedMs,
    double ThroughputLogsPerSec,
    double P50LatencyMs,
    double P95LatencyMs);

public sealed record QueryMetric(
    double ElapsedMs,
    int ReturnedCount,
    int TotalCount);

public sealed record StorageFactoryResult(
    bool Enabled,
    ILogStorage? Storage,
    IDisposable? Disposable,
    IAsyncDisposable? AsyncDisposable,
    string? Note);
