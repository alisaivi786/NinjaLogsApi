using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Configuration;

public sealed class StorageNodeLeaseService(StorageOptions storageOptions) : IAsyncDisposable
{
    private readonly StorageOptions _storageOptions = storageOptions;
    private FileStream? _leaseStream;

    public Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        string provider = _storageOptions.GetNormalizedProvider();
        if (provider is not ("file" or "segmentedfile"))
        {
            return Task.CompletedTask;
        }

        string lockRoot = provider == "file"
            ? Path.GetFullPath(_storageOptions.LogsDirectory)
            : Path.GetFullPath(_storageOptions.SegmentedFile.DataDirectory);

        Directory.CreateDirectory(lockRoot);
        string lockFilePath = Path.Combine(lockRoot, ".ninjalogs.writer.lock");

        try
        {
            _leaseStream = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using StreamWriter writer = new(_leaseStream, leaveOpen: true);
            writer.WriteLine($"{Environment.MachineName}|{Environment.ProcessId}|{DateTime.UtcNow:O}");
            writer.Flush();
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Writer lease acquisition failed for provider '{_storageOptions.Provider}'. " +
                "Another instance is already writing to this storage. Use SQL providers for multi-node deployments.",
                ex);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _leaseStream?.Dispose();
        _leaseStream = null;
        return ValueTask.CompletedTask;
    }
}
