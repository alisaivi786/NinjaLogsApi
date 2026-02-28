namespace NinjaLogs.Api.Configuration;

public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";

    public string Tier { get; set; } = "Community";
    public bool IsLicensed { get; set; } = false;
}
