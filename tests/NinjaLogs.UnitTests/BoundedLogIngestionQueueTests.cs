using NinjaLogs.Modules.Logging.Application.Services;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;

namespace NinjaLogs.UnitTests;

public sealed class BoundedLogIngestionQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_ShouldPreserveEventOrder()
    {
        BoundedLogIngestionQueue queue = new(capacity: 10);
        LogEvent first = new(DateTime.UtcNow, LogLevel.Information, "first", null, null, null, null);
        LogEvent second = new(DateTime.UtcNow, LogLevel.Error, "second", null, null, null, null);

        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        LogEvent actualFirst = await queue.DequeueAsync();
        LogEvent actualSecond = await queue.DequeueAsync();

        Assert.Equal("first", actualFirst.Message);
        Assert.Equal("second", actualSecond.Message);
    }
}
