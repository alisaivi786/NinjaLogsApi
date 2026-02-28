using NinjaLogs.Modules.Logging.Application.Interfaces;

namespace NinjaLogs.Api.Configuration;

public sealed class ProviderLogIndexStrategy(string provider, IReadOnlyCollection<string> descriptors) : ILogIndexStrategy
{
    public string Provider { get; } = provider;
    public IReadOnlyCollection<string> Descriptors { get; } = descriptors;
}
