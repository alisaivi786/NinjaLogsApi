namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Models;

public sealed record SegmentManifest(
    int Version,
    int ActiveSegmentNumber,
    IReadOnlyCollection<SegmentInfo> Segments);
