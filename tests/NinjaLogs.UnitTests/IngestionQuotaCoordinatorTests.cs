using NinjaLogs.Api.Configuration;

namespace NinjaLogs.UnitTests;

public sealed class IngestionQuotaCoordinatorTests
{
    [Fact]
    public async Task EnterAsync_ShouldSerializeConcurrentAccess()
    {
        IngestionQuotaCoordinator coordinator = new();
        int inside = 0;
        int maxInside = 0;

        async Task Work()
        {
            using IDisposable gate = await coordinator.EnterAsync();
            inside++;
            maxInside = Math.Max(maxInside, inside);
            await Task.Delay(25);
            inside--;
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Work()));

        Assert.Equal(1, maxInside);
    }
}
