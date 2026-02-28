namespace NinjaLogs.Api.Configuration;

public sealed class IngestionQuotaCoordinator
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        return new Releaser(_gate);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;
        public void Dispose()
        {
            SemaphoreSlim? s = Interlocked.Exchange(ref _semaphore, null);
            s?.Release();
        }
    }
}
