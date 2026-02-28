using NinjaLogs.Api.Configuration;
using NinjaLogs.Modules.Logging.Infrastructure.Options;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.Configure<IngestionApiKeyOptions>(builder.Configuration.GetSection(IngestionApiKeyOptions.SectionName));
builder.Services.AddSingleton<IngestionApiKeyValidator>();
builder.Services.Configure<LicensingOptions>(builder.Configuration.GetSection(LicensingOptions.SectionName));
builder.Services.AddLoggingStorage(builder.Configuration);

StorageOptions storageOptions = builder.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new();
LicensingOptions licensingOptions = builder.Configuration.GetSection(LicensingOptions.SectionName).Get<LicensingOptions>() ?? new();
StoragePolicyEnforcer.ValidateOrThrow(storageOptions, licensingOptions);
DeploymentProfileValidator.ValidateOrThrow(storageOptions);
builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton(licensingOptions);
builder.Services.AddScoped<StorageQuotaService>();
builder.Services.AddScoped<StorageSchemaBootstrapper>();
builder.Services.AddSingleton<StorageNodeLeaseService>();

var app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    StorageSchemaBootstrapper bootstrapper = scope.ServiceProvider.GetRequiredService<StorageSchemaBootstrapper>();
    await bootstrapper.EnsureInitializedAsync();
}

StorageNodeLeaseService leaseService = app.Services.GetRequiredService<StorageNodeLeaseService>();
await leaseService.AcquireAsync();

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
