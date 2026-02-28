namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

public static class SegmentPathResolver
{
    public static string BuildSegmentFileName(int number) => $"segment-{number:D5}.dat";
}
