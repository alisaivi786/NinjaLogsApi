using Microsoft.AspNetCore.Mvc;
using NinjaLogs.Modules.Logging.Infrastructure.Options;

namespace NinjaLogs.Api.Controllers;

[Route("api/v1.0/diagnostics")]
[Route("api/diagnostics")]
public sealed class DiagnosticsController(StorageOptions storageOptions) : BaseApiController
{
    [HttpGet("storage")]
    public IActionResult GetStorage()
    {
        return Ok(new
        {
            provider = storageOptions.Provider,
            connectionString = storageOptions.ConnectionString,
            logsDirectory = storageOptions.LogsDirectory,
            currentDirectory = Directory.GetCurrentDirectory(),
            baseDirectory = AppContext.BaseDirectory
        });
    }
}
