using NinjaLogs.Modules.Logging.Domain.Entities;
using NinjaLogs.Modules.Logging.Domain.Enums;
using NinjaLogs.Modules.Logging.Domain.Models;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using NinjaLogs.Modules.Logging.Infrastructure.Storage.File;

namespace NinjaLogs.UnitTests;

public sealed class FileLogStorageTests
{
    [Fact]
    public async Task QueryAsync_ShouldApplySearchTextAgainstMessageAndProperties()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ninjalogs-file-ut", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            FileLogStorage storage = new(new StorageOptions
            {
                Provider = "File",
                LogsDirectory = tempDir
            });

            await storage.AppendAsync(new LogEvent(DateTime.UtcNow, LogLevel.Information, "payment succeeded", "Svc", "Test", null, "{\"orderId\":\"123\"}"));
            await storage.AppendAsync(new LogEvent(DateTime.UtcNow, LogLevel.Warning, "inventory low", "Svc", "Test", null, "{\"sku\":\"A1\"}"));

            PagedResult<LogEvent> result = await storage.QueryAsync(new LogQuery(SearchText: "orderId", Page: 1, PageSize: 10));

            LogEvent item = Assert.Single(result.Items);
            Assert.Contains("payment", item.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
