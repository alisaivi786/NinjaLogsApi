namespace NinjaLogs.Api.Configuration;

public sealed class IngestionApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    public string[] IngestionKeys { get; set; } = [];
}
