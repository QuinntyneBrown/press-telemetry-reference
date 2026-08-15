using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Telemetry.Ingestion.Worker;

/// <summary>Composes the ingestion worker host; shared by Program and the integration tests.</summary>
public static class WorkerHost
{
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();

        builder.Services.AddOptions<WorkerOptions>()
            .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<WorkerOptions>, WorkerOptionsValidator>();

        builder.Services.AddSingleton<TelemetryPointParser>();
        builder.Services.AddSingleton<MqttTelemetrySource>();
        builder.Services.AddSingleton<CouchbaseTimeSeriesWriter>();
        builder.Services.AddSingleton<RedisTelemetryPublisher>();
        builder.Services.AddHostedService<TelemetryIngestionWorker>();

        return builder;
    }
}
