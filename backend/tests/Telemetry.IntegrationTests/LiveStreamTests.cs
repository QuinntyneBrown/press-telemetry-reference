using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>
/// L2-007 (broadcast hub) and L2-008 (worker fan-out via the Redis backplane) — live points
/// reach every connected client of every API instance without a Couchbase read.
/// </summary>
[Collection(TelemetryCollection.Name)]
public sealed class LiveStreamTests(TelemetryEnvironment environment) : IAsyncLifetime
{
    private IHost _worker = null!;
    private TestLogCollector _logs = null!;
    private ApiFactory _api = null!;

    public async Task InitializeAsync()
    {
        (_worker, _logs) = await TestWorker.StartAsync(environment);
        _api = new ApiFactory(environment);
        await TestWorker.WaitUntilSubscribedAsync(_logs);
    }

    public async Task DisposeAsync()
    {
        await TestWorker.StopAsync(_worker);
        _api.Dispose();
    }

    // L2-007 AC1 and AC2: every connected client receives each ingested point within 1 second.
    [Fact]
    public async Task All_connected_clients_receive_an_ingested_point_within_one_second()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        await using var client1 = await ConnectClientAsync(_api);
        await using var client2 = await ConnectClientAsync(_api);
        await using var client3 = await ConnectClientAsync(_api);
        var received = new[] { Expect(client1.Connection), Expect(client2.Connection), Expect(client3.Connection) };

        await environment.PublishMqttAsync($"telemetry/{deviceId}",
            $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":87.4,"timestamp":"2026-08-15T10:00:00Z"}""");

        foreach (var expectation in received)
        {
            var point = await expectation.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(deviceId, point.GetProperty("deviceId").GetString());
            Assert.Equal("temperature", point.GetProperty("metric").GetString());
            Assert.Equal(87.4, point.GetProperty("value").GetDouble());
            Assert.Equal(DateTimeOffset.Parse("2026-08-15T10:00:00Z"), point.GetProperty("timestamp").GetDateTimeOffset());
        }
    }

    // L2-007 AC3: a client connecting mid-stream receives only points ingested afterwards.
    [Fact]
    public async Task A_late_joining_client_receives_only_points_ingested_after_it_connected()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        await environment.PublishMqttAsync($"telemetry/{deviceId}",
            $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":1,"timestamp":"2026-08-15T10:00:00Z"}""");
        await Task.Delay(500);

        await using var client = await ConnectClientAsync(_api);
        var received = new List<JsonElement>();
        client.Connection.On<JsonElement>("telemetry", received.Add);
        await Task.Delay(500);
        Assert.Empty(received);

        await environment.PublishMqttAsync($"telemetry/{deviceId}",
            $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":2,"timestamp":"2026-08-15T10:01:00Z"}""");
        await Poll.UntilAsync(() => Task.FromResult(received.Count == 1), TimeSpan.FromSeconds(5), "the post-connect point arrives");
        Assert.Equal(2, received[0].GetProperty("value").GetDouble());
    }

    // L2-008 AC1: with two API instances, one connected client each, both clients receive the point.
    [Fact]
    public async Task Clients_of_two_separate_api_instances_both_receive_the_point()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        using var secondApi = new ApiFactory(environment);
        await using var clientA = await ConnectClientAsync(_api);
        await using var clientB = await ConnectClientAsync(secondApi);
        var expectations = new[] { Expect(clientA.Connection), Expect(clientB.Connection) };

        await environment.PublishMqttAsync($"telemetry/{deviceId}",
            $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":5,"timestamp":"2026-08-15T10:00:00Z"}""");

        foreach (var expectation in expectations)
        {
            var point = await expectation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(deviceId, point.GetProperty("deviceId").GetString());
        }
    }

    // L2-008 AC2: live delivery does not depend on Couchbase.
    [Fact]
    public async Task Live_delivery_continues_while_couchbase_is_unavailable()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        await using var client = await ConnectClientAsync(_api);
        var expectation = Expect(client.Connection);

        await environment.PauseCouchbaseAsync();
        try
        {
            await environment.PublishMqttAsync($"telemetry/{deviceId}",
                $$"""{"deviceId":"{{deviceId}}","metric":"temperature","value":9,"timestamp":"2026-08-15T10:00:00Z"}""");
            var point = await expectation.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(9, point.GetProperty("value").GetDouble());
        }
        finally
        {
            await environment.UnpauseCouchbaseAsync();
        }
    }

    private static Task<JsonElement> Expect(HubConnection connection)
    {
        var received = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>("telemetry", point => received.TrySetResult(point));
        return received.Task;
    }

    private static async Task<LiveClient> ConnectClientAsync(ApiFactory api)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/telemetry", options =>
            {
                options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await connection.StartAsync();
        return new LiveClient(connection);
    }
}
