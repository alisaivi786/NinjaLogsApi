using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Models;

namespace NinjaLogs.UnitTests;

public sealed class SegmentWriterTests
{
    [Fact]
    public async Task AppendAsync_ShouldRotate_WhenSegmentLimitIsExceeded()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ninjalogs-segment-writer-ut", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            SegmentWriter writer = new(tempDir, segmentMaxBytes: 400);
            SegmentManifest manifest = new(
                Version: 1,
                ActiveSegmentNumber: 1,
                Segments:
                [
                    new SegmentInfo(1, "segment-00001.dat", 0, false, DateTime.UtcNow, DateTime.UtcNow)
                ]);

            for (int i = 0; i < 10; i++)
            {
                manifest = await writer.AppendAsync(manifest, new LogEvent(
                    DateTime.UtcNow,
                    LogLevel.Information,
                    $"Writer test event {i} {new string('x', 100)}",
                    "Svc",
                    "Test",
                    null,
                    null), CancellationToken.None);
            }

            Assert.True(manifest.Segments.Count > 1);
            Assert.True(manifest.ActiveSegmentNumber > 1);
            Assert.True(Directory.EnumerateFiles(tempDir, "segment-*.dat").Count() > 1);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
