using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>
/// L2-001 (subscribe), L2-002 (validation), L2-003 (time series writes) — the ingestion
/// pipeline from a published MQTT message to points served by the API.
/// </summary>
[Collection(TelemetryCollection.Name)]
public sealed class IngestionTests(TelemetryEnvironment environment) : IAsyncLifetime
{
    private IHost _worker = null!;
    private TestLogCollector _logs = null!;
    private ApiFactory _api = null!;
    private HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        (_worker, _logs) = await TestWorker.StartAsync(environment);
        _api = new ApiFactory(environment);
        _http = _api.CreateClient();
        await TestWorker.WaitUntilSubscribedAsync(_logs);
    }

    public async Task DisposeAsync()
    {
        await TestWorker.StopAsync(_worker);
        _http.Dispose();
        _api.Dispose();
    }

    // L2-001 AC1, L2-002 AC1, L2-003 AC1: a valid point is accepted, persisted to the hourly
    // time series document, and served back with its original value and timestamp.
    [Fact]
    public async Task Valid_point_flows_from_mqtt_through_couchbase_to_the_api()
    {
        Assert.Contains(_logs.Entries, entry => entry.Message.Contains(environment.MqttBrokerUri));
        Assert.Contains(_logs.Entries, entry => entry.Message.Contains("telemetry/#"));

        var deviceId = TelemetryEnvironment.Unique("press");
        await environment.PublishMqttAsync($"telemetry/{deviceId}",
            $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":87.4,"timestamp":"2026-08-15T10:00:00Z"}""");

        JsonElement points = default;
        await Poll.UntilAsync(async () =>
        {
            points = await _http.GetFromJsonAsync<JsonElement>(RangeUrl(deviceId));
            return points.GetArrayLength() == 1;
        }, TimeSpan.FromSeconds(10), "published point retrievable through the API");

        Assert.Equal(87.4, points[0].GetProperty("value").GetDouble());
        Assert.Equal(DateTimeOffset.Parse("2026-08-15T10:00:00Z"), points[0].GetProperty("timestamp").GetDateTimeOffset());

        var latest = await _http.GetFromJsonAsync<JsonElement>("/api/telemetry/latest");
        Assert.Contains(latest.EnumerateArray(), entry => entry.GetProperty("deviceId").GetString() == deviceId);
    }

    // L2-002 AC2 and AC3: each invalid shape is dropped with a warning, writes nothing, pushes
    // nothing, and later valid messages flow normally.
    [Fact]
    public async Task Invalid_messages_are_dropped_with_warnings_while_valid_messages_continue()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        await using var redis = await ConnectionMultiplexer.ConnectAsync(environment.RedisConnectionString);
        var pushed = new ConcurrentBag<string>();
        await redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal("telemetry"), (_, message) => pushed.Add(message!));

        var topic = $"telemetry/{deviceId}";
        await environment.PublishMqttAsync(topic, "{not json");
        await environment.PublishMqttAsync(topic, $$"""{"deviceId":"{{deviceId}}","value":1,"timestamp":"2026-08-15T10:00:00Z"}""");
        await environment.PublishMqttAsync(topic, $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":1e999,"timestamp":"2026-08-15T10:00:00Z"}""");
        await environment.PublishMqttAsync(topic, $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":1,"timestamp":"not-a-time"}""");
        await environment.PublishMqttAsync(topic, $$"""{"deviceId":"has space","metric":"temperature","value":1,"timestamp":"2026-08-15T10:00:00Z"}""");
        await environment.PublishMqttAsync(topic, $$"""{"deviceId":"{{deviceId}}","metric":"{{new string('m', 65)}}","value":1,"timestamp":"2026-08-15T10:00:00Z"}""");
        await environment.PublishMqttAsync(topic,
            $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":42.0,"timestamp":"2026-08-15T10:05:00Z"}""");

        JsonElement points = default;
        await Poll.UntilAsync(async () =>
        {
            points = await _http.GetFromJsonAsync<JsonElement>(RangeUrl(deviceId));
            return points.GetArrayLength() > 0;
        }, TimeSpan.FromSeconds(10), "the valid message following the invalid burst is ingested");

        // Only the one valid point was stored for this series.
        Assert.Equal(1, points.GetArrayLength());
        Assert.Equal(42.0, points[0].GetProperty("value").GetDouble());

        // Every invalid message produced a warning naming a reason.
        var warnings = _logs.Entries.Where(entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(warnings.Count >= 6, $"expected at least 6 drop warnings, saw {warnings.Count}");

        // Nothing invalid was pushed to the live path; the valid point was.
        Assert.Single(pushed, message => message.Contains(deviceId));
    }

    // L2-003 AC2 and AC3: 1,000 points spanning two hourly buckets are all retrievable exactly
    // once, ascending, via a _timeseries() range query (through the API).
    [Fact]
    public async Task Ingests_1000_points_across_two_hourly_buckets_exactly_once_in_order()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        var start = DateTimeOffset.Parse("2026-08-15T10:59:00Z");
        using var client = await environment.ConnectMqttClientAsync();
        for (var i = 0; i < 1000; i++)
        {
            var timestamp = start.AddMilliseconds(i * 200); // crosses the 11:00 bucket boundary
            await client.PublishAsync(TelemetryEnvironment.BuildMessage($"telemetry/{deviceId}",
                $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":{{i}},"timestamp":"{{timestamp:O}}"}"""));
        }

        JsonElement points = default;
        await Poll.UntilAsync(async () =>
        {
            points = await _http.GetFromJsonAsync<JsonElement>(RangeUrl(deviceId, "2026-08-15T10:59:00Z", "2026-08-15T11:59:00Z"));
            return points.GetArrayLength() == 1000;
        }, TimeSpan.FromSeconds(60), "all 1,000 published points retrievable through the API");

        Assert.Equal(
            Enumerable.Range(0, 1000).Select(i => (double)i),
            points.EnumerateArray().Select(point => point.GetProperty("value").GetDouble()));
    }

    private static string RangeUrl(string deviceId, string from = "2026-08-15T10:00:00Z", string to = "2026-08-15T11:00:00Z")
        => $"/api/telemetry/{deviceId}/temperature?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}";
}
