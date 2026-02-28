using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.UnitTests;

public sealed class IngestionProtectionServiceTests
{
    [Fact]
    public void IsPayloadAllowed_ShouldReject_WhenOverLimit()
    {
        StorageOptions options = new()
        {
            IngestionPipeline = new IngestionPipelineOptions
            {
                MaxPayloadBytes = 100
            }
        };
        IngestionProtectionService service = new(options);

        bool allowed = service.IsPayloadAllowed(101, out long maxBytes);

        Assert.False(allowed);
        Assert.Equal(100, maxBytes);
    }

    [Fact]
    public void TryConsume_ShouldRateLimit_WhenExceeded()
    {
        StorageOptions options = new()
        {
            IngestionPipeline = new IngestionPipelineOptions
            {
                MaxRequestsPerMinutePerApiKey = 2
            }
        };
        IngestionProtectionService service = new(options);

        Assert.True(service.TryConsume("k1", out _));
        Assert.True(service.TryConsume("k1", out _));
        Assert.False(service.TryConsume("k1", out int retryAfterSeconds));
        Assert.True(retryAfterSeconds > 0);
    }
}
