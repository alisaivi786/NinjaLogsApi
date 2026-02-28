using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Models;
using System.Text.Json;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

public sealed class SegmentWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _dataDirectory;
    private readonly long _segmentMaxBytes;

    public SegmentWriter(string dataDirectory, long segmentMaxBytes)
    {
        _dataDirectory = dataDirectory;
        _segmentMaxBytes = segmentMaxBytes;
    }

    public async Task<SegmentManifest> AppendAsync(SegmentManifest manifest, LogEvent logEvent, CancellationToken cancellationToken)
    {
        SegmentInfo active = manifest.Segments.Single(s => s.SegmentNumber == manifest.ActiveSegmentNumber);
        string serialized = JsonSerializer.Serialize(logEvent with { TimestampUtc = logEvent.TimestampUtc.ToUniversalTime() }, JsonOptions) + Environment.NewLine;
        long payloadBytes = System.Text.Encoding.UTF8.GetByteCount(serialized);

        if (active.SizeBytes + payloadBytes > _segmentMaxBytes)
        {
            manifest = Rotate(manifest, active);
            active = manifest.Segments.Single(s => s.SegmentNumber == manifest.ActiveSegmentNumber);
        }

        string segmentPath = Path.Combine(_dataDirectory, active.FileName);
        await System.IO.File.AppendAllTextAsync(segmentPath, serialized, cancellationToken);

        SegmentInfo updatedActive = active with
        {
            SizeBytes = active.SizeBytes + payloadBytes,
            LastWriteUtc = DateTime.UtcNow
        };

        IReadOnlyCollection<SegmentInfo> updatedSegments = manifest.Segments
            .Select(s => s.SegmentNumber == updatedActive.SegmentNumber ? updatedActive : s)
            .ToList();

        return manifest with { Segments = updatedSegments };
    }

    private static SegmentManifest Rotate(SegmentManifest manifest, SegmentInfo active)
    {
        SegmentInfo sealedActive = active with { Sealed = true, LastWriteUtc = DateTime.UtcNow };
        int nextNumber = active.SegmentNumber + 1;
        SegmentInfo next = new(
            SegmentNumber: nextNumber,
            FileName: SegmentPathResolver.BuildSegmentFileName(nextNumber),
            SizeBytes: 0,
            Sealed: false,
            CreatedUtc: DateTime.UtcNow,
            LastWriteUtc: DateTime.UtcNow);

        List<SegmentInfo> segments = manifest.Segments
            .Select(s => s.SegmentNumber == active.SegmentNumber ? sealedActive : s)
            .ToList();
        segments.Add(next);

        return manifest with
        {
            ActiveSegmentNumber = nextNumber,
            Segments = segments
        };
    }
}
