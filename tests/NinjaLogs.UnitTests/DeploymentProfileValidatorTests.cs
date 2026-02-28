using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.UnitTests;

public sealed class DeploymentProfileValidatorTests
{
    [Fact]
    public void ShouldAllow_SingleNode_File_SingleWriter()
    {
        StorageOptions options = new()
        {
            Provider = "File",
            Deployment = new DeploymentOptions { Profile = "SingleNode", NodeMode = "SingleWriter" }
        };

        DeploymentProfileValidator.ValidateOrThrow(options);
    }

    [Fact]
    public void ShouldThrow_OnPremHa_File()
    {
        StorageOptions options = new()
        {
            Provider = "File",
            Deployment = new DeploymentOptions { Profile = "OnPremHa", NodeMode = "MultiWriter" },
            Retention = new StorageRetentionOptions { Enabled = true }
        };

        Assert.Throws<InvalidOperationException>(() => DeploymentProfileValidator.ValidateOrThrow(options));
    }

    [Fact]
    public void ShouldThrow_CloudHa_SingleWriter()
    {
        StorageOptions options = new()
        {
            Provider = "PostgreSQL",
            Connections = new StorageConnectionOptions { PostgreSQL = "Host=localhost;Database=x;Username=u;Password=p" },
            Deployment = new DeploymentOptions { Profile = "CloudHa", NodeMode = "SingleWriter" },
            Retention = new StorageRetentionOptions { Enabled = true }
        };

        Assert.Throws<InvalidOperationException>(() => DeploymentProfileValidator.ValidateOrThrow(options));
    }

    [Fact]
    public void ShouldThrow_WhenDatabaseProviderRetentionDisabled()
    {
        StorageOptions options = new()
        {
            Provider = "SqlServer",
            Connections = new StorageConnectionOptions { SqlServer = "Server=.;Database=x;User ID=sa;Password=p;" },
            Retention = new StorageRetentionOptions { Enabled = false }
        };

        Assert.Throws<InvalidOperationException>(() => DeploymentProfileValidator.ValidateOrThrow(options));
    }

    [Fact]
    public void ShouldThrow_WhenDatabaseProviderConnectionMissing()
    {
        StorageOptions options = new()
        {
            Provider = "SQLite",
            Retention = new StorageRetentionOptions { Enabled = true }
        };

        Assert.Throws<InvalidOperationException>(() => DeploymentProfileValidator.ValidateOrThrow(options));
    }
}
