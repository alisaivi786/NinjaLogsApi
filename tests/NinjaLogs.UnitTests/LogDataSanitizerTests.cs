using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;

namespace NinjaLogs.UnitTests;

public sealed class LogDataSanitizerTests
{
    [Fact]
    public void Sanitize_ShouldMaskSensitiveKeys()
    {
        LogDataSanitizer sanitizer = new();
        LogEvent input = new(
            DateTime.UtcNow,
            LogLevel.Error,
            "msg",
            null, null, null,
            "{\"password\":\"abc\",\"token\":\"xyz\"}",
            RequestHeadersJson: "{\"authorization\":\"Bearer 123\",\"apiKey\":\"K\"}",
            RequestBody: "{\"pwd\":\"123\"}",
            ResponseBody: "{\"secret\":\"v\"}");

        LogEvent output = sanitizer.Sanitize(input);

        Assert.Contains("\"password\":\"***\"", output.PropertiesJson);
        Assert.Contains("\"token\":\"***\"", output.PropertiesJson);
        Assert.Contains("\"authorization\":\"***\"", output.RequestHeadersJson);
        Assert.Contains("\"apiKey\":\"***\"", output.RequestHeadersJson);
        Assert.Contains("\"pwd\":\"***\"", output.RequestBody);
        Assert.Contains("\"secret\":\"***\"", output.ResponseBody);
    }

    [Fact]
    public void Sanitize_ShouldTruncateLargeBodies()
    {
        LogDataSanitizer sanitizer = new();
        string large = new('x', 9000);
        LogEvent input = new(DateTime.UtcNow, LogLevel.Information, "m", null, null, null, null, RequestBody: large, ResponseBody: large);

        LogEvent output = sanitizer.Sanitize(input);
        Assert.Equal(8000, output.RequestBody!.Length);
        Assert.Equal(8000, output.ResponseBody!.Length);
    }
}
