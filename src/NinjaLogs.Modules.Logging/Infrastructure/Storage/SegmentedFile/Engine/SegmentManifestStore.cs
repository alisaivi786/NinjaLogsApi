using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Models;
using System.Text.Json;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

public sealed class SegmentManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _manifestPath;

    public SegmentManifestStore(string manifestPath)
    {
        _manifestPath = manifestPath;
    }

    public async Task<SegmentManifest> LoadOrCreateAsync(string dataDirectory, CancellationToken cancellationToken)
    {
        if (!System.IO.File.Exists(_manifestPath))
        {
            SegmentManifest initial = CreateInitial();
            await SaveAsync(initial, cancellationToken);
            return initial;
        }

        await using FileStream stream = System.IO.File.OpenRead(_manifestPath);
        SegmentManifest? manifest = await JsonSerializer.DeserializeAsync<SegmentManifest>(stream, JsonOptions, cancellationToken);
        if (manifest is null || manifest.Segments.Count == 0)
        {
            SegmentManifest initial = CreateInitial();
            await SaveAsync(initial, cancellationToken);
            return initial;
        }

        return await RecoverSegmentSizesAsync(dataDirectory, manifest, cancellationToken);
    }

    public async Task SaveAsync(SegmentManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_manifestPath)!);
        await using FileStream stream = System.IO.File.Create(_manifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken);
    }

    private static SegmentManifest CreateInitial()
    {
        SegmentInfo first = new(
            SegmentNumber: 1,
            FileName: SegmentPathResolver.BuildSegmentFileName(1),
            SizeBytes: 0,
            Sealed: false,
            CreatedUtc: DateTime.UtcNow,
            LastWriteUtc: DateTime.UtcNow);

        return new SegmentManifest(1, 1, [first]);
    }

    private static Task<SegmentManifest> RecoverSegmentSizesAsync(string dataDirectory, SegmentManifest manifest, CancellationToken cancellationToken)
    {
        List<SegmentInfo> recovered = [];
        foreach (SegmentInfo segment in manifest.Segments.OrderBy(s => s.SegmentNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.Combine(dataDirectory, segment.FileName);
            long size = System.IO.File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            recovered.Add(segment with { SizeBytes = size });
        }

        SegmentManifest updated = manifest with { Segments = recovered };
        return Task.FromResult(updated);
    }
}
