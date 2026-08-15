using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telemetry.Ingestion.Worker;

namespace Telemetry.IntegrationTests.Infrastructure;

/// <summary>Runs the real ingestion worker host in-process against the container environment.</summary>
public static class TestWorker
{
    public static async Task<(IHost Host, TestLogCollector Logs)> StartAsync(
        TelemetryEnvironment environment, Action<Dictionary<string, string?>>? configure = null)
    {
        var logs = new TestLogCollector();
        var settings = new Dictionary<string, string?>
        {
            ["Worker:MqttBroker"] = environment.MqttBrokerUri,
            ["Worker:MqttTopicFilter"] = "telemetry/#",
            ["Worker:CouchbaseConnectionString"] = environment.CouchbaseConnectionString,
            ["Worker:CouchbaseUsername"] = TelemetryEnvironment.CouchbaseUsername,
            ["Worker:CouchbasePassword"] = TelemetryEnvironment.CouchbasePassword,
            ["Worker:CouchbaseBucket"] = TelemetryEnvironment.CouchbaseBucket,
            ["Worker:RedisConnectionString"] = environment.RedisConnectionString,
        };
        configure?.Invoke(settings);

        var builder = WorkerHost.CreateBuilder([]);
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Logging.AddProvider(logs);
        var host = builder.Build();
        await host.StartAsync();
        return (host, logs);
    }

    public static Task WaitUntilSubscribedAsync(TestLogCollector logs, TimeSpan? timeout = null)
        => Poll.UntilAsync(
            () => Task.FromResult(logs.Entries.Any(entry => entry.Message.Contains("Subscribed", StringComparison.OrdinalIgnoreCase))),
            timeout ?? TimeSpan.FromSeconds(20), "worker subscribed to the MQTT topic filter");

    public static async Task StopAsync(IHost host)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await host.StopAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
        }

        host.Dispose();
    }
}
