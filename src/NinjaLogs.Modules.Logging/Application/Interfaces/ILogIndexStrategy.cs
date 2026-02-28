namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface ILogIndexStrategy
{
    string Provider { get; }
    IReadOnlyCollection<string> Descriptors { get; }
}
