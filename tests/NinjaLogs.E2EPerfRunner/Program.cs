using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

string baseUrl = (Environment.GetEnvironmentVariable("PERF_BASE_URL") ?? "http://localhost:8085").TrimEnd('/');
string apiKey = Environment.GetEnvironmentVariable("PERF_API_KEY") ?? "dev-ingestion-key";
int concurrency = Math.Max(1, GetInt("PERF_CONCURRENCY", 20));
int durationSeconds = Math.Max(5, GetInt("PERF_DURATION_SECONDS", 60));
int payloadPaddingBytes = Math.Max(0, GetInt("PERF_PAYLOAD_PADDING_BYTES", 300));
int diagnosticsIntervalMs = Math.Max(250, GetInt("PERF_DIAGNOSTICS_INTERVAL_MS", 1000));
string reportPath =
    args.FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("PERF_REPORT_PATH")
    ?? Path.Combine("tests", "perf", "reports", $"e2e-performance-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");

DateTime startedUtc = DateTime.UtcNow;
string runId = Guid.NewGuid().ToString("N")[..12];

using HttpClient http = new()
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(30)
};
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

CancellationTokenSource cts = new(TimeSpan.FromSeconds(durationSeconds));
CancellationToken token = cts.Token;

ConcurrentBag<double> latenciesMs = [];
long totalRequests = 0;
long accepted202 = 0;
long throttled429 = 0;
long payloadTooLarge413 = 0;
long unauthorized401 = 0;
long otherErrors = 0;
long requestExceptions = 0;

List<QueueDepthSample> queueSamples = [];
object sampleLock = new();

Task diagnosticsTask = Task.Run(async () =>
{
    while (!token.IsCancellationRequested)
    {
        QueueDepthSample? sample = await TryFetchQueueSampleAsync(http, token);
        if (sample is not null)
        {
            lock (sampleLock)
            {
                queueSamples.Add(sample);
            }
        }

        await Task.Delay(diagnosticsIntervalMs, token);
    }
}, token);

Task[] workers = Enumerable.Range(0, concurrency)
    .Select(workerId => Task.Run(async () =>
    {
        int i = 0;
        while (!token.IsCancellationRequested)
        {
            string payload = BuildPayload(workerId, i++, payloadPaddingBytes);
            using StringContent content = new(payload, Encoding.UTF8, "application/json");

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                using HttpResponseMessage response = await http.PostAsync("/api/v1.0/logs", content, token);
                sw.Stop();
                latenciesMs.Add(sw.Elapsed.TotalMilliseconds);

                Interlocked.Increment(ref totalRequests);
                int code = (int)response.StatusCode;
                if (code == (int)HttpStatusCode.Accepted)
                {
                    Interlocked.Increment(ref accepted202);
                }
                else if (code == (int)HttpStatusCode.TooManyRequests)
                {
                    Interlocked.Increment(ref throttled429);
                }
                else if (code == 413)
                {
                    Interlocked.Increment(ref payloadTooLarge413);
                }
                else if (code == (int)HttpStatusCode.Unauthorized)
                {
                    Interlocked.Increment(ref unauthorized401);
                }
                else
                {
                    Interlocked.Increment(ref otherErrors);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                sw.Stop();
                latenciesMs.Add(sw.Elapsed.TotalMilliseconds);
                Interlocked.Increment(ref totalRequests);
                Interlocked.Increment(ref requestExceptions);
            }
        }
    }, token)).ToArray();

try
{
    await Task.WhenAll(workers);
}
catch (OperationCanceledException)
{
}

try
{
    await diagnosticsTask;
}
catch (OperationCanceledException)
{
}

DateTime finishedUtc = DateTime.UtcNow;
double elapsedSeconds = Math.Max(1, (finishedUtc - startedUtc).TotalSeconds);

List<double> sortedLatency = latenciesMs.ToList();
sortedLatency.Sort();

QueueDepthStats queueDepthStats;
lock (sampleLock)
{
    queueDepthStats = BuildQueueDepthStats(queueSamples);
}

RuntimeSnapshot? endRuntime = await TryFetchRuntimeSnapshotAsync(http, CancellationToken.None);

E2EPerformanceReport report = new(
    RunId: runId,
    StartedUtc: startedUtc,
    FinishedUtc: finishedUtc,
    BaseUrl: baseUrl,
    Concurrency: concurrency,
    DurationSeconds: durationSeconds,
    PayloadPaddingBytes: payloadPaddingBytes,
    Requests: new RequestStats(
        TotalRequests: totalRequests,
        Accepted202: accepted202,
        Throttled429: throttled429,
        PayloadTooLarge413: payloadTooLarge413,
        Unauthorized401: unauthorized401,
        OtherErrors: otherErrors,
        RequestExceptions: requestExceptions,
        AcceptedLogsPerSec: accepted202 / elapsedSeconds,
        TotalRequestsPerSec: totalRequests / elapsedSeconds,
        P50LatencyMs: Percentile(sortedLatency, 0.50),
        P95LatencyMs: Percentile(sortedLatency, 0.95),
        P99LatencyMs: Percentile(sortedLatency, 0.99)),
    QueueDepth: queueDepthStats,
    EndRuntime: endRuntime);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, jsonOptions));

Console.WriteLine($"E2E report written: {Path.GetFullPath(reportPath)}");
Console.WriteLine($"Accepted logs/sec: {report.Requests.AcceptedLogsPerSec:F2}");
Console.WriteLine($"P95 latency (ms): {report.Requests.P95LatencyMs:F2}");
Console.WriteLine($"Queue depth avg/max: {report.QueueDepth.Average:F2}/{report.QueueDepth.Max}");
if (report.EndRuntime is not null)
{
    Console.WriteLine($"Runtime writtenPerSecond: {report.EndRuntime.WrittenPerSecond:F2}");
    Console.WriteLine($"Runtime p95IngestionLatencyMs: {report.EndRuntime.P95IngestionLatencyMs:F2}");
    Console.WriteLine($"Runtime p95DbInsertDurationMs: {report.EndRuntime.P95DbInsertDurationMs:F2}");
}

static string BuildPayload(int workerId, int iteration, int payloadPaddingBytes)
{
    string pad = new('x', payloadPaddingBytes);
    var payload = new
    {
        timestampUtc = DateTime.UtcNow,
        level = "Error",
        message = "phase4 e2e benchmark",
        serviceName = "Perf",
        environment = "Perf",
        traceId = $"W{workerId}-I{iteration}",
        statusCode = 500,
        requestMethod = "POST",
        propertiesJson = JsonSerializer.Serialize(new { pad, phase = 4, kind = "e2e" })
    };

    return JsonSerializer.Serialize(payload);
}

static async Task<QueueDepthSample?> TryFetchQueueSampleAsync(HttpClient http, CancellationToken token)
{
    try
    {
        using HttpResponseMessage response = await http.GetAsync("/api/v1.0/diagnostics/storage-health", token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (!doc.RootElement.TryGetProperty("queueDepth", out JsonElement queueDepthElement) ||
            queueDepthElement.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        int queueDepth = queueDepthElement.GetInt32();
        return new QueueDepthSample(DateTime.UtcNow, queueDepth);
    }
    catch
    {
        return null;
    }
}

static async Task<RuntimeSnapshot?> TryFetchRuntimeSnapshotAsync(HttpClient http, CancellationToken token)
{
    try
    {
        using HttpResponseMessage response = await http.GetAsync("/api/v1.0/diagnostics/storage", token);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        if (!doc.RootElement.TryGetProperty("metrics", out JsonElement metrics))
        {
            return null;
        }

        return new RuntimeSnapshot(
            Written: GetLong(metrics, "written"),
            WrittenPerSecond: GetDouble(metrics, "writtenPerSecond"),
            WriteFailures: GetLong(metrics, "writeFailures"),
            P95IngestionLatencyMs: GetDouble(metrics, "p95IngestionLatencyMs"),
            P95DbInsertDurationMs: GetDouble(metrics, "p95DbInsertDurationMs"));
    }
    catch
    {
        return null;
    }
}

static QueueDepthStats BuildQueueDepthStats(List<QueueDepthSample> samples)
{
    if (samples.Count == 0)
    {
        return new QueueDepthStats(0, 0, 0, 0, []);
    }

    int min = samples.Min(s => s.Depth);
    int max = samples.Max(s => s.Depth);
    double avg = samples.Average(s => s.Depth);
    int last = samples[^1].Depth;
    return new QueueDepthStats(min, max, avg, last, samples);
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

static int GetInt(string env, int fallback)
{
    string? raw = Environment.GetEnvironmentVariable(env);
    return int.TryParse(raw, out int value) ? value : fallback;
}

static long GetLong(JsonElement element, string property)
{
    return element.TryGetProperty(property, out JsonElement p) && p.ValueKind == JsonValueKind.Number
        ? p.GetInt64()
        : 0;
}

static double GetDouble(JsonElement element, string property)
{
    return element.TryGetProperty(property, out JsonElement p) && p.ValueKind == JsonValueKind.Number
        ? p.GetDouble()
        : 0;
}

public sealed record E2EPerformanceReport(
    string RunId,
    DateTime StartedUtc,
    DateTime FinishedUtc,
    string BaseUrl,
    int Concurrency,
    int DurationSeconds,
    int PayloadPaddingBytes,
    RequestStats Requests,
    QueueDepthStats QueueDepth,
    RuntimeSnapshot? EndRuntime);

public sealed record RequestStats(
    long TotalRequests,
    long Accepted202,
    long Throttled429,
    long PayloadTooLarge413,
    long Unauthorized401,
    long OtherErrors,
    long RequestExceptions,
    double AcceptedLogsPerSec,
    double TotalRequestsPerSec,
    double P50LatencyMs,
    double P95LatencyMs,
    double P99LatencyMs);

public sealed record QueueDepthStats(
    int Min,
    int Max,
    double Average,
    int Last,
    IReadOnlyList<QueueDepthSample> Samples);

public sealed record QueueDepthSample(DateTime TimestampUtc, int Depth);

public sealed record RuntimeSnapshot(
    long Written,
    double WrittenPerSecond,
    long WriteFailures,
    double P95IngestionLatencyMs,
    double P95DbInsertDurationMs);
