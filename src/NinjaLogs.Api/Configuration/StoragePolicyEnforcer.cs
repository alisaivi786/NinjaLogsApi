using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Configuration;

public static class StoragePolicyEnforcer
{
    public static void ValidateOrThrow(StorageOptions storage, LicensingOptions licensing)
    {
        string provider = storage.GetNormalizedProvider();

        if (storage.Retention.RetainDays <= 0)
        {
            throw new InvalidOperationException("Storage:Retention:RetainDays must be greater than 0.");
        }

        if (storage.Policy.EnforceForDatabaseProviders && IsDatabaseProvider(provider))
        {
            if (storage.Policy.RequireRetentionEnabledForDatabaseProviders && !storage.Retention.Enabled)
            {
                throw new InvalidOperationException(
                    $"Retention must be enabled for database provider '{storage.Provider}'. Set Storage:Retention:Enabled=true.");
            }

            int maxRetention = licensing.IsLicensed
                ? storage.Policy.LicensedMaxRetentionDays
                : storage.Policy.UnlicensedMaxRetentionDays;

            if (storage.Retention.RetainDays > maxRetention)
            {
                throw new InvalidOperationException(
                    $"Retention days ({storage.Retention.RetainDays}) exceeds allowed maximum ({maxRetention}) for current license tier '{licensing.Tier}'.");
            }
        }

        if (storage.Policy.EnforceLicenseSegmentCap && provider == "segmentedfile")
        {
            long maxSegment = licensing.IsLicensed
                ? storage.Policy.LicensedSegmentMaxBytes
                : storage.Policy.UnlicensedSegmentMaxBytes;

            if (storage.SegmentedFile.SegmentMaxBytes > maxSegment)
            {
                throw new InvalidOperationException(
                    $"SegmentMaxBytes ({storage.SegmentedFile.SegmentMaxBytes}) exceeds allowed maximum ({maxSegment}) for current license tier '{licensing.Tier}'.");
            }
        }
    }

    private static bool IsDatabaseProvider(string provider) =>
        provider is "sqlite" or "sqlserver" or "postgresql";
}
