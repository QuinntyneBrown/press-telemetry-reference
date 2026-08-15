using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telemetry.Ingestion.Worker;
using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>L2-018 AC2/AC3 (config precedence, fail fast) and L2-021 AC3 (log level filtering).</summary>
[Collection(TelemetryCollection.Name)]
public sealed class WorkerConfigurationTests(TelemetryEnvironment environment)
{
    [Fact]
    public async Task Worker_fails_fast_naming_the_missing_setting()
    {
        var startup = await Record.ExceptionAsync(() => TestWorker.StartAsync(environment,
            settings => settings["Worker:MqttBroker"] = ""));

        Assert.NotNull(startup);
        Assert.Contains("Worker:MqttBroker", startup.ToString());
    }

    // L2-018 AC2: an environment variable override takes precedence over committed defaults.
    [Fact]
    public void Environment_variable_overrides_take_precedence_over_committed_defaults()
    {
        Environment.SetEnvironmentVariable("Worker__MqttTopicFilter", "overridden/#");
        try
        {
            using var host = WorkerHost.CreateBuilder([]).Build();
            var options = host.Services.GetRequiredService<IOptions<WorkerOptions>>().Value;
            Assert.Equal("overridden/#", options.MqttTopicFilter);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Worker__MqttTopicFilter", null);
        }
    }

    // L2-021 AC3: at Warning level, per-message ingestion logs are suppressed while warnings
    // still appear.
    [Fact]
    public async Task Warning_log_level_suppresses_per_message_logs_but_keeps_warnings()
    {
        var (worker, logs) = await TestWorker.StartAsync(environment,
            settings => settings["Logging:LogLevel:Default"] = "Warning");
        try
        {
            var deviceId = TelemetryEnvironment.Unique("press");
            using var api = new ApiFactory(environment);
            using var http = api.CreateClient();

            // The "subscribed" info log is suppressed, so ingestion itself proves readiness.
            await Poll.UntilAsync(async () =>
            {
                await environment.PublishMqttAsync($"telemetry/{deviceId}",
                    $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":1,"timestamp":"2026-08-15T10:00:00Z"}""");
                var points = await http.GetFromJsonAsync<JsonElement>(
                    $"/api/telemetry/{deviceId}/temperature?from=2026-08-15T10%3A00%3A00Z&to=2026-08-15T11%3A00%3A00Z");
                return points.GetArrayLength() > 0;
            }, TimeSpan.FromSeconds(20), "a point ingested while logging at Warning level");

            Assert.DoesNotContain(logs.Entries, entry =>
                entry.Level < LogLevel.Warning && entry.Category.StartsWith("Telemetry"));

            await environment.PublishMqttAsync($"telemetry/{deviceId}", "{not json");
            await Poll.UntilAsync(
                () => Task.FromResult(logs.Entries.Any(entry => entry.Level == LogLevel.Warning)),
                TimeSpan.FromSeconds(10), "a warning for an invalid message still appears");
        }
        finally
        {
            await TestWorker.StopAsync(worker);
        }
    }
}
