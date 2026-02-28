using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.UnitTests;

public sealed class StoragePolicyEnforcerTests
{
    [Fact]
    public void ValidateOrThrow_ShouldThrow_WhenDatabaseProviderAndRetentionDisabled()
    {
        StorageOptions storage = CreateDefaultStorage();
        storage.Provider = "SqlServer";
        storage.Retention.Enabled = false;
        storage.Policy.RequireRetentionEnabledForDatabaseProviders = true;

        LicensingOptions licensing = new() { IsLicensed = false, Tier = "Community" };

        Assert.Throws<InvalidOperationException>(() =>
            StoragePolicyEnforcer.ValidateOrThrow(storage, licensing));
    }

    [Fact]
    public void ValidateOrThrow_ShouldThrow_WhenUnlicensedRetentionExceedsCap()
    {
        StorageOptions storage = CreateDefaultStorage();
        storage.Provider = "SQLite";
        storage.Retention.Enabled = true;
        storage.Retention.RetainDays = 90;
        storage.Policy.UnlicensedMaxRetentionDays = 30;

        LicensingOptions licensing = new() { IsLicensed = false, Tier = "Community" };

        Assert.Throws<InvalidOperationException>(() =>
            StoragePolicyEnforcer.ValidateOrThrow(storage, licensing));
    }

    [Fact]
    public void ValidateOrThrow_ShouldNotThrow_WhenLicensedAndWithinCap()
    {
        StorageOptions storage = CreateDefaultStorage();
        storage.Provider = "SqlServer";
        storage.Retention.Enabled = true;
        storage.Retention.RetainDays = 365;
        storage.Policy.LicensedMaxRetentionDays = 3650;

        LicensingOptions licensing = new() { IsLicensed = true, Tier = "Enterprise" };

        StoragePolicyEnforcer.ValidateOrThrow(storage, licensing);
    }

    [Fact]
    public void ValidateOrThrow_ShouldThrow_WhenSegmentedFileExceedsUnlicensedSegmentCap()
    {
        StorageOptions storage = CreateDefaultStorage();
        storage.Provider = "SegmentedFile";
        storage.Policy.EnforceLicenseSegmentCap = true;
        storage.Policy.UnlicensedSegmentMaxBytes = 256L * 1024 * 1024;
        storage.SegmentedFile.SegmentMaxBytes = 1024L * 1024 * 1024;

        LicensingOptions licensing = new() { IsLicensed = false, Tier = "Community" };

        Assert.Throws<InvalidOperationException>(() =>
            StoragePolicyEnforcer.ValidateOrThrow(storage, licensing));
    }

    private static StorageOptions CreateDefaultStorage() => new()
    {
        Provider = "File",
        Retention = new StorageRetentionOptions
        {
            Enabled = true,
            RetainDays = 30
        },
        Policy = new StoragePolicyOptions
        {
            EnforceForDatabaseProviders = true,
            RequireRetentionEnabledForDatabaseProviders = true,
            UnlicensedMaxRetentionDays = 30,
            LicensedMaxRetentionDays = 3650
        },
        SegmentedFile = new SegmentedFileStorageOptions
        {
            SegmentMaxBytes = 256L * 1024 * 1024
        }
    };
}
