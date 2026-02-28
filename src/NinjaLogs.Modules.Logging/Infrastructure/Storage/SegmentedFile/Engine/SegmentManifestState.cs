using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Models;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

public sealed class SegmentManifestState
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SegmentManifestState(SegmentManifest manifest)
    {
        Manifest = manifest;
    }

    public SegmentManifest Manifest { get; private set; }

    public async Task<T> UpdateAsync<T>(Func<SegmentManifest, Task<(SegmentManifest Manifest, T Result)>> mutation)
    {
        await _gate.WaitAsync();
        try
        {
            (SegmentManifest manifest, T result) = await mutation(Manifest);
            Manifest = manifest;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
