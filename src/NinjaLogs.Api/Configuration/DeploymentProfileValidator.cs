using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Configuration;

public static class DeploymentProfileValidator
{
    public static void ValidateOrThrow(StorageOptions storage)
    {
        string provider = storage.GetNormalizedProvider();
        string nodeMode = (storage.Deployment.NodeMode ?? "SingleWriter").Trim().ToLowerInvariant();
        string profile = (storage.Deployment.Profile ?? "SingleNode").Trim().ToLowerInvariant();

        if ((provider == "file" || provider == "segmentedfile") && nodeMode != "singlewriter")
        {
            throw new InvalidOperationException(
                $"Provider '{storage.Provider}' only supports Deployment:NodeMode=SingleWriter.");
        }

        if ((provider == "sqlite" || provider == "sqlserver" || provider == "postgresql") &&
            !storage.Retention.Enabled)
        {
            throw new InvalidOperationException(
                $"Provider '{storage.Provider}' requires Storage:Retention:Enabled=true for enterprise profiles.");
        }

        if (provider == "sqlserver" && string.IsNullOrWhiteSpace(storage.Connections.SqlServer) && string.IsNullOrWhiteSpace(storage.ConnectionString))
        {
            throw new InvalidOperationException("SQL Server provider requires Storage:Connections:SqlServer or Storage:ConnectionString.");
        }

        if (provider == "postgresql" && string.IsNullOrWhiteSpace(storage.Connections.PostgreSQL) && string.IsNullOrWhiteSpace(storage.ConnectionString))
        {
            throw new InvalidOperationException("PostgreSQL provider requires Storage:Connections:PostgreSQL or Storage:ConnectionString.");
        }

        if (provider == "sqlite" && string.IsNullOrWhiteSpace(storage.Connections.SQLite) && string.IsNullOrWhiteSpace(storage.ConnectionString))
        {
            throw new InvalidOperationException("SQLite provider requires Storage:Connections:SQLite or Storage:ConnectionString.");
        }

        if (profile is "onpremha" or "cloudha")
        {
            if (provider is "file" or "segmentedfile")
            {
                throw new InvalidOperationException($"Deployment profile '{storage.Deployment.Profile}' does not allow provider '{storage.Provider}'. Use SQL Server or PostgreSQL.");
            }

            if (nodeMode != "multiwriter")
            {
                throw new InvalidOperationException($"Deployment profile '{storage.Deployment.Profile}' requires Deployment:NodeMode=MultiWriter.");
            }
        }
    }
}
