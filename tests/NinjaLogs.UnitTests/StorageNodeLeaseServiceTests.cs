using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.UnitTests;

public sealed class StorageNodeLeaseServiceTests
{
    [Fact]
    public async Task AcquireAsync_ShouldSucceed_ForFileProviderSingleInstance()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ninjalogs-lease-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        StorageOptions options = new()
        {
            Provider = "File",
            LogsDirectory = dir
        };

        await using StorageNodeLeaseService lease = new(options);
        await lease.AcquireAsync();
    }

    [Fact]
    public async Task AcquireAsync_ShouldThrow_WhenSecondFileWriterStarts()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ninjalogs-lease-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        StorageOptions options = new()
        {
            Provider = "File",
            LogsDirectory = dir
        };

        await using StorageNodeLeaseService first = new(options);
        await first.AcquireAsync();

        await using StorageNodeLeaseService second = new(options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.AcquireAsync());
    }
}
