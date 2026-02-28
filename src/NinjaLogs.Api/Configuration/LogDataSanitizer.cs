using System.Text.RegularExpressions;
using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Api.Configuration;

public sealed class LogDataSanitizer
{
    private static readonly Regex SensitiveJsonRegex =
        new("(password|passwd|pwd|token|authorization|secret|api[_-]?key)\"\\s*:\\s*\"[^\"]*\"",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public LogEvent Sanitize(LogEvent input)
    {
        return input with
        {
            RequestHeadersJson = SanitizeJsonLike(input.RequestHeadersJson),
            ResponseHeadersJson = SanitizeJsonLike(input.ResponseHeadersJson),
            RequestBody = SanitizeBody(input.RequestBody),
            ResponseBody = SanitizeBody(input.ResponseBody),
            PropertiesJson = SanitizeJsonLike(input.PropertiesJson)
        };
    }

    private static string? SanitizeJsonLike(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return SensitiveJsonRegex.Replace(value, "$1\":\"***\"");
    }

    private static string? SanitizeBody(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        string sanitized = SanitizeJsonLike(value) ?? value;
        return sanitized.Length <= 8000 ? sanitized : sanitized[..8000];
    }
}
