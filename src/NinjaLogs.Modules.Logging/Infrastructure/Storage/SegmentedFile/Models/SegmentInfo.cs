namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Models;

public sealed record SegmentInfo(
    int SegmentNumber,
    string FileName,
    long SizeBytes,
    bool Sealed,
    DateTime CreatedUtc,
    DateTime LastWriteUtc);
