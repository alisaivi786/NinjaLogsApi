using NinjaLogs.Modules.Logging.Domain.Entities;

namespace NinjaLogs.Modules.Logging.Application.Interfaces;

public sealed record IngestionQueueItem(
    long Sequence,
    DateTime EnqueuedUtc,
    LogEvent Event);
