using Microsoft.Extensions.Options;

namespace NinjaLogs.Api.Configuration;

public sealed class IngestionApiKeyValidator(IOptions<IngestionApiKeyOptions> options)
{
    private readonly HashSet<string> _keys = options.Value.IngestionKeys
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x.Trim())
        .ToHashSet(StringComparer.Ordinal);

    public bool IsValid(string? apiKey) =>
        !string.IsNullOrWhiteSpace(apiKey) && _keys.Contains(apiKey.Trim());
}
