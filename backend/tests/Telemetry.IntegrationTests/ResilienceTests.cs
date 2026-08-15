using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>
/// L2-001 (MQTT reconnection), L2-004 (persistence failure handling), L2-008 AC4 (Redis
/// outage), L2-021 AC2 (structured connection-state logs) — outages never kill the worker
/// and every path recovers.
/// </summary>
[Collection(TelemetryCollection.Name)]
public sealed class ResilienceTests(TelemetryEnvironment environment)
{
    // L2-001 AC2 and AC3: a dropped broker connection is retried with logged state changes,
    // and after recovery the worker re-subscribes and ingests without a restart.
    [Fact]
    public async Task Worker_reconnects_and_resubscribes_after_a_broker_outage()
    {
        var (worker, logs) = await TestWorker.StartAsync(environment);
        try
        {
            await TestWorker.WaitUntilSubscribedAsync(logs);

            await environment.StopMqttBrokerAsync();
            await Poll.UntilAsync(
                () => Task.FromResult(logs.Entries.Any(entry =>
                    entry.Level == LogLevel.Warning && entry.Message.Contains("retrying", StringComparison.OrdinalIgnoreCase))),
                TimeSpan.FromSeconds(30), "a logged reconnect attempt");

            var subscribedBeforeRecovery = logs.Entries.Count(entry => entry.Message.Contains("Subscribed"));
            await environment.StartMqttBrokerAsync();
            await Poll.UntilAsync(
                () => Task.FromResult(logs.Entries.Count(entry => entry.Message.Contains("Subscribed")) > subscribedBeforeRecovery),
                TimeSpan.FromSeconds(60), "re-subscription after broker recovery");

            // L2-021 AC2: structured entries recorded the disconnect and each retry.
            Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Warning
                && (entry.Message.Contains("lost", StringComparison.OrdinalIgnoreCase)
                    || entry.Message.Contains("failed", StringComparison.OrdinalIgnoreCase)));

            // Messages published after re-subscription are ingested without a restart.
            var deviceId = TelemetryEnvironment.Unique("press");
            await environment.PublishMqttAsync($"telemetry/{deviceId}",
                $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":7,"timestamp":"2026-08-15T10:00:00Z"}""");
            using var api = new ApiFactory(environment);
            using var http = api.CreateClient();
            await Poll.UntilAsync(async () =>
            {
                var points = await http.GetFromJsonAsync<JsonElement>(RangeUrl(deviceId));
                return points.GetArrayLength() == 1;
            }, TimeSpan.FromSeconds(10), "a point ingested after broker recovery");
        }
        finally
        {
            await environment.StartMqttBrokerAsync();
            await TestWorker.StopAsync(worker);
        }
    }

    // L2-001 AC4: an unreachable broker at startup means retrying, never exiting.
    [Fact]
    public async Task Worker_keeps_retrying_when_the_broker_is_unreachable_at_startup()
    {
        await environment.StopMqttBrokerAsync();
        var (worker, logs) = await TestWorker.StartAsync(environment);
        try
        {
            await Poll.UntilAsync(
                () => Task.FromResult(logs.Entries.Count(entry =>
                    entry.Level == LogLevel.Warning && entry.Message.Contains("retrying", StringComparison.OrdinalIgnoreCase)) >= 2),
                TimeSpan.FromSeconds(30), "repeated connection retries while the broker is down");

            await environment.StartMqttBrokerAsync();
            await TestWorker.WaitUntilSubscribedAsync(logs, TimeSpan.FromSeconds(60));
        }
        finally
        {
            await environment.StartMqttBrokerAsync();
            await TestWorker.StopAsync(worker);
        }
    }

    // L2-004: a failing Couchbase write is retried 3 times with backoff, dropped with an error
    // naming the point, and later points flow normally once Couchbase recovers.
    [Fact]
    public async Task Persistence_failures_drop_the_point_after_retries_without_stalling_the_pipeline()
    {
        var (worker, logs) = await TestWorker.StartAsync(environment);
        try
        {
            await TestWorker.WaitUntilSubscribedAsync(logs);
            var droppedDevice = TelemetryEnvironment.Unique("press");
            var survivorDevice = TelemetryEnvironment.Unique("press");

            await environment.PauseCouchbaseAsync();
            try
            {
                await environment.PublishMqttAsync($"telemetry/{droppedDevice}",
                    $$"""{"deviceId":"{{droppedDevice}}","metric":"temperature","value":1,"timestamp":"2026-08-15T10:00:00Z"}""");
                await Poll.UntilAsync(
                    () => Task.FromResult(logs.Entries.Any(entry =>
                        entry.Level == LogLevel.Error && entry.Message.Contains(droppedDevice))),
                    TimeSpan.FromSeconds(90), "an error log naming the dropped point");
                Assert.Contains(logs.Entries, entry => entry.Level == LogLevel.Error
                    && entry.Message.Contains(droppedDevice)
                    && entry.Message.Contains("temperature")
                    && entry.Message.Contains("10:00:00"));
            }
            finally
            {
                await environment.UnpauseCouchbaseAsync();
            }

            // Subsequent valid points are processed normally after recovery.
            using var api = new ApiFactory(environment);
            using var http = api.CreateClient();
            await Poll.UntilAsync(async () =>
            {
                await environment.PublishMqttAsync($"telemetry/{survivorDevice}",
                    $$"""{"deviceId":"{{survivorDevice}}","metric":"temperature","value":2,"timestamp":"2026-08-15T10:00:00Z"}""");
                var points = await http.GetFromJsonAsync<JsonElement>(RangeUrl(survivorDevice));
                return points.GetArrayLength() > 0;
            }, TimeSpan.FromSeconds(30), "a point ingested after Couchbase recovery");

            // The failed point was dropped, not queued.
            var dropped = await http.GetFromJsonAsync<JsonElement>(RangeUrl(droppedDevice));
            Assert.Equal(0, dropped.GetArrayLength());
        }
        finally
        {
            await TestWorker.StopAsync(worker);
        }
    }

    // L2-008 AC4: during a Redis outage points are persisted and the fan-out failure is
    // logged; delivery resumes for newly ingested points after recovery, with no replay.
    [Fact]
    public async Task Redis_outage_pauses_live_delivery_but_never_persistence()
    {
        var (worker, logs) = await TestWorker.StartAsync(environment);
        using var api = new ApiFactory(environment);
        using var http = api.CreateClient();
        try
        {
            await TestWorker.WaitUntilSubscribedAsync(logs);
            var deviceId = TelemetryEnvironment.Unique("press");

            var connection = new HubConnectionBuilder()
                .WithUrl("http://localhost/hubs/telemetry", options =>
                {
                    options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
                .Build();
            var received = new List<double>();
            connection.On<JsonElement>("telemetry", point =>
            {
                if (point.GetProperty("deviceId").GetString() == deviceId)
                {
                    lock (received)
                    {
                        received.Add(point.GetProperty("value").GetDouble());
                    }
                }
            });
            await connection.StartAsync();

            await environment.PauseRedisAsync();
            try
            {
                await environment.PublishMqttAsync($"telemetry/{deviceId}",
                    $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":1,"timestamp":"2026-08-15T10:00:00Z"}""");
                await Poll.UntilAsync(
                    () => Task.FromResult(logs.Entries.Any(entry =>
                        entry.Level == LogLevel.Warning && entry.Message.Contains("fan-out", StringComparison.OrdinalIgnoreCase))),
                    TimeSpan.FromSeconds(30), "a logged live fan-out failure");
            }
            finally
            {
                await environment.UnpauseRedisAsync();
            }

            // The point published during the outage was persisted.
            await Poll.UntilAsync(async () =>
            {
                var points = await http.GetFromJsonAsync<JsonElement>(RangeUrl(deviceId));
                return points.GetArrayLength() == 1;
            }, TimeSpan.FromSeconds(15), "the outage-time point persisted to Couchbase");

            // Live delivery resumes for newly ingested points (the outage point is not replayed).
            var next = 2.0;
            await Poll.UntilAsync(async () =>
            {
                await environment.PublishMqttAsync($"telemetry/{deviceId}",
                    $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":{{next++}},"timestamp":"2026-08-15T10:01:00Z"}""");
                await Task.Delay(500);
                lock (received)
                {
                    return received.Count > 0;
                }
            }, TimeSpan.FromSeconds(45), "live delivery resumed after Redis recovery");

            lock (received)
            {
                Assert.DoesNotContain(1.0, received);
            }

            await connection.DisposeAsync();
        }
        finally
        {
            await TestWorker.StopAsync(worker);
        }
    }

    private static string RangeUrl(string deviceId)
        => $"/api/telemetry/{deviceId}/temperature?from={Uri.EscapeDataString("2026-08-15T10:00:00Z")}&to={Uri.EscapeDataString("2026-08-15T11:00:00Z")}";
}
