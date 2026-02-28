using Microsoft.Extensions.Options;
using NinjaLogs.Api.Configuration;

namespace NinjaLogs.UnitTests;

public sealed class IngestionApiKeyValidatorTests
{
    [Fact]
    public void IsValid_ShouldReturnTrue_ForConfiguredKey()
    {
        IngestionApiKeyOptions options = new()
        {
            IngestionKeys = ["dev-ingestion-key", "another-key"]
        };

        IngestionApiKeyValidator validator = new(Options.Create(options));

        Assert.True(validator.IsValid("dev-ingestion-key"));
    }

    [Fact]
    public void IsValid_ShouldReturnFalse_ForMissingKey()
    {
        IngestionApiKeyOptions options = new()
        {
            IngestionKeys = ["dev-ingestion-key"]
        };

        IngestionApiKeyValidator validator = new(Options.Create(options));

        Assert.False(validator.IsValid("not-configured"));
    }
}
