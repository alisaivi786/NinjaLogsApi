using NinjaLogs.Modules.Logging.Application.Interfaces;
using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile.Engine;

namespace NinjaLogs.Modules.Logging.Infrastructure.Storage.SegmentedFile;

public sealed class SegmentedFileLogStorage : ILogStorage
{
    private readonly SegmentManifestStore _manifestStore;
    private readonly SegmentWriter _writer;
    private readonly SegmentQueryReader _reader;
    private readonly string _dataDirectory;
    private SegmentManifestState? _state;

    public SegmentedFileLogStorage(StorageOptions options)
    {
        string baseDir = string.IsNullOrWhiteSpace(options.SegmentedFile.DataDirectory)
            ? "data"
            : options.SegmentedFile.DataDirectory;
        _dataDirectory = Path.GetFullPath(baseDir);
        Directory.CreateDirectory(_dataDirectory);

        string manifestFileName = string.IsNullOrWhiteSpace(options.SegmentedFile.ManifestFileName)
            ? "manifest.json"
            : options.SegmentedFile.ManifestFileName;
        _manifestStore = new SegmentManifestStore(Path.Combine(_dataDirectory, manifestFileName));
        _writer = new SegmentWriter(_dataDirectory, options.SegmentedFile.SegmentMaxBytes);
        _reader = new SegmentQueryReader(_dataDirectory);
    }

    public async Task AppendAsync(LogEvent log, CancellationToken cancellationToken = default)
    {
        SegmentManifestState state = await GetStateAsync(cancellationToken);
        await state.UpdateAsync(async manifest =>
        {
            var updated = await _writer.AppendAsync(manifest, log, cancellationToken);
            await _manifestStore.SaveAsync(updated, cancellationToken);
            return (updated, true);
        });
    }

    public async Task<PagedResult<LogEvent>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        SegmentManifestState state = await GetStateAsync(cancellationToken);
        return await _reader.QueryAsync(state, query, cancellationToken);
    }

    private async Task<SegmentManifestState> GetStateAsync(CancellationToken cancellationToken)
    {
        if (_state is not null) return _state;

        var manifest = await _manifestStore.LoadOrCreateAsync(_dataDirectory, cancellationToken);
        _state = new SegmentManifestState(manifest);
        return _state;
    }
}
