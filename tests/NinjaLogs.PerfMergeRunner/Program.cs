using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: dotnet run --project tests/NinjaLogs.PerfMergeRunner/NinjaLogs.PerfMergeRunner.csproj -- <providerReportPath> <e2eReportPath> <combinedReportPath>");
    return;
}

string providerPath = args[0];
string e2ePath = args[1];
string combinedPath = args[2];

JsonNode providerRoot = JsonNode.Parse(await File.ReadAllTextAsync(providerPath))
    ?? throw new InvalidOperationException("Could not parse provider report JSON.");
JsonNode e2eRoot = JsonNode.Parse(await File.ReadAllTextAsync(e2ePath))
    ?? throw new InvalidOperationException("Could not parse E2E report JSON.");

JsonObject? e2eRequests = e2eRoot["Requests"]?.AsObject();
JsonObject? e2eQueue = e2eRoot["QueueDepth"]?.AsObject();
JsonObject? e2eRuntime = e2eRoot["EndRuntime"]?.AsObject();

double acceptedLogsPerSec = GetDouble(e2eRequests, "AcceptedLogsPerSec");
double p95LatencyMs = GetDouble(e2eRequests, "P95LatencyMs");
double queueAvg = GetDouble(e2eQueue, "Average");
double queueMax = GetDouble(e2eQueue, "Max");
double runtimeWrittenPerSec = GetDouble(e2eRuntime, "WrittenPerSecond");
double runtimeP95IngestionLatency = GetDouble(e2eRuntime, "P95IngestionLatencyMs");
double runtimeP95DbInsert = GetDouble(e2eRuntime, "P95DbInsertDurationMs");

JsonArray checks =
[
    BuildCheck("accepted_logs_per_sec>=2000", acceptedLogsPerSec >= 2000, $"actual={acceptedLogsPerSec:F2}"),
    BuildCheck("request_p95_latency_ms<150", p95LatencyMs > 0 && p95LatencyMs < 150, $"actual={p95LatencyMs:F2}"),
    BuildCheck("runtime_written_per_sec>=2000", runtimeWrittenPerSec >= 2000, $"actual={runtimeWrittenPerSec:F2}"),
    BuildCheck("runtime_p95_ingestion_latency_ms<150", runtimeP95IngestionLatency > 0 && runtimeP95IngestionLatency < 150, $"actual={runtimeP95IngestionLatency:F2}"),
    BuildCheck("queue_depth_not_exploding", queueMax <= Math.Max(5000, queueAvg * 10), $"avg={queueAvg:F2},max={queueMax:F2}")
];

JsonObject summary = new()
{
    ["GeneratedUtc"] = DateTime.UtcNow,
    ["Phase4Target"] = new JsonObject
    {
        ["LogsPerSecondPerNode"] = 2000,
        ["P95IngestionLatencyMsUpperBound"] = 150
    },
    ["QuickView"] = new JsonObject
    {
        ["AcceptedLogsPerSec"] = acceptedLogsPerSec,
        ["RequestP95LatencyMs"] = p95LatencyMs,
        ["QueueDepthAverage"] = queueAvg,
        ["QueueDepthMax"] = queueMax,
        ["RuntimeWrittenPerSecond"] = runtimeWrittenPerSec,
        ["RuntimeP95IngestionLatencyMs"] = runtimeP95IngestionLatency,
        ["RuntimeP95DbInsertDurationMs"] = runtimeP95DbInsert
    },
    ["Checks"] = checks
};

JsonObject combined = new()
{
    ["Summary"] = summary,
    ["ProviderReport"] = providerRoot,
    ["E2EReport"] = e2eRoot
};

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(combinedPath))!);
await File.WriteAllTextAsync(combinedPath, JsonSerializer.Serialize(combined, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"Combined report written: {Path.GetFullPath(combinedPath)}");

static JsonObject BuildCheck(string name, bool pass, string detail)
{
    return new JsonObject
    {
        ["Name"] = name,
        ["Pass"] = pass,
        ["Detail"] = detail
    };
}

static double GetDouble(JsonObject? obj, string property)
{
    if (obj is null || !obj.TryGetPropertyValue(property, out JsonNode? node) || node is null)
    {
        return 0;
    }

    try
    {
        return node.GetValue<double>();
    }
    catch
    {
        return 0;
    }
}
