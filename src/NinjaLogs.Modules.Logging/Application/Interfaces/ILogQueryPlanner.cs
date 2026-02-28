using NinjaLogs.Modules.Logging.Domain.Models;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public interface ILogQueryPlanner
{
    LogQuery Normalize(LogQuery query);
}
