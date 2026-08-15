using System.Text;
using Couchbase;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MQTTnet;
using Testcontainers.Couchbase;
using Testcontainers.Redis;

namespace Telemetry.IntegrationTests.Infrastructure;

/// <summary>
/// Starts the three external infrastructure services (Couchbase, Redis, MQTT broker) once per
/// test run and initializes the telemetry bucket with a primary index.
/// </summary>
public sealed class TelemetryEnvironment : IAsyncLifetime
{
    public const string CouchbaseUsername = "Administrator";
    public const string CouchbasePassword = "password";
    public const string CouchbaseBucket = "telemetry";

    private readonly CouchbaseContainer _couchbase = new CouchbaseBuilder("couchbase:enterprise-7.6.4").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4").Build();
    // A fixed host port keeps the broker address stable across the stop/start cycles the
    // reconnection tests perform (a random binding would be reassigned on restart).
    private readonly int _mqttHostPort = FreeTcpPort();
    private readonly IContainer _mosquitto;

    public TelemetryEnvironment()
    {
        _mosquitto = new ContainerBuilder("eclipse-mosquitto:2.0")
            .WithResourceMapping(Encoding.UTF8.GetBytes("listener 1883\nallow_anonymous true\n"), "/mosquitto/config/mosquitto.conf")
            .WithPortBinding(_mqttHostPort, 1883)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(1883))
            .Build();
    }

    private static int FreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public ICluster Cluster { get; private set; } = null!;

    public string CouchbaseConnectionString => _couchbase.GetConnectionString();
    public string RedisConnectionString => _redis.GetConnectionString();
    public string MqttBrokerUri => $"mqtt://{_mosquitto.Hostname}:{_mqttHostPort}";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_couchbase.StartAsync(), _redis.StartAsync(), _mosquitto.StartAsync());

        // Enterprise images require an indexer storage mode before any index can be created.
        using (var management = new HttpClient())
        {
            management.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{CouchbaseUsername}:{CouchbasePassword}")));
            var response = await management.PostAsync(
                $"http://{_couchbase.Hostname}:{_couchbase.GetMappedPublicPort(8091)}/settings/indexes",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["storageMode"] = "plasma" }));
            response.EnsureSuccessStatusCode();
        }

        Cluster = await Couchbase.Cluster.ConnectAsync(
            CouchbaseConnectionString, CouchbaseUsername, CouchbasePassword);
        await Cluster.Buckets.CreateBucketAsync(new BucketSettings
        {
            Name = CouchbaseBucket,
            BucketType = BucketType.Couchbase,
            RamQuotaMB = 100,
            NumReplicas = 0,
            FlushEnabled = true,
        });
        await Poll.UntilAsync(async () =>
        {
            var bucket = await Cluster.BucketAsync(CouchbaseBucket);
            await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
            return true;
        }, TimeSpan.FromSeconds(90), "telemetry bucket ready");

        // The query/index services need a moment before they accept index DDL for a new bucket.
        await Poll.UntilAsync(async () =>
        {
            await Cluster.QueryIndexes.CreatePrimaryIndexAsync(CouchbaseBucket,
                new Couchbase.Management.Query.CreatePrimaryQueryIndexOptions().IgnoreIfExists(true));
            return true;
        }, TimeSpan.FromSeconds(90), "primary index on telemetry bucket");
    }

    public async Task DisposeAsync()
    {
        if (Cluster is not null)
        {
            await Cluster.DisposeAsync();
        }

        await Task.WhenAll(_couchbase.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask(), _mosquitto.DisposeAsync().AsTask());
    }

    /// <summary>
    /// Removes every stored telemetry point, for tests that need an empty store. Uses a
    /// query DELETE rather than a bucket flush: a flush triggers a GSI indexer rollback that
    /// makes consistent queries fail transiently.
    /// </summary>
    public async Task FlushTelemetryAsync()
    {
        var result = await Cluster.QueryAsync<object>($"DELETE FROM `{CouchbaseBucket}`",
            queryOptions => queryOptions.ScanConsistency(Couchbase.Query.QueryScanConsistency.RequestPlus));
        _ = await result.Rows.ToListAsync();
    }

    /// <summary>
    /// Seeds a stored point directly, using the same hourly time series document format the
    /// ingestion worker writes, so API reads can be tested without running the worker.
    /// </summary>
    public async Task SeedPointAsync(string deviceId, string metric, DateTimeOffset timestamp, double value)
    {
        var bucket = await Cluster.BucketAsync(CouchbaseBucket);
        var collection = bucket.DefaultCollection();
        var hourStart = new DateTimeOffset(timestamp.UtcDateTime.Date).AddHours(timestamp.UtcDateTime.Hour);
        var key = $"{deviceId}::{metric}::{hourStart:yyyy-MM-ddTHH}";
        await collection.MutateInAsync(key, specs =>
            {
                specs.Upsert("deviceId", deviceId);
                specs.Upsert("metric", metric);
                specs.Upsert("ts_start", hourStart.ToUnixTimeMilliseconds());
                specs.Upsert("ts_end", hourStart.AddHours(1).ToUnixTimeMilliseconds() - 1);
                specs.ArrayAppend("ts_data", new[] { new object[] { timestamp.ToUnixTimeMilliseconds(), value } }, true);
            },
            options => options.StoreSemantics(Couchbase.KeyValue.StoreSemantics.Upsert));
    }

    /// <summary>A unique, shape-valid deviceId/metric segment so tests do not interfere.</summary>
    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    /// <summary>Publishes a raw MQTT message the way the external telemetry publisher would.</summary>
    public async Task PublishMqttAsync(string topic, string payload)
    {
        using var client = await ConnectMqttClientAsync();
        await client.PublishAsync(BuildMessage(topic, payload));
        await client.DisconnectAsync();
    }

    /// <summary>A connected MQTT client for tests that publish many messages.</summary>
    public async Task<IMqttClient> ConnectMqttClientAsync()
    {
        var uri = new Uri(MqttBrokerUri);
        var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(uri.Host, uri.Port).Build());
        return client;
    }

    public static MqttApplicationMessage BuildMessage(string topic, string payload)
        => new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(payload))
            .Build();

    public Task StopMqttBrokerAsync() => _mosquitto.StopAsync();
    public Task StartMqttBrokerAsync() => _mosquitto.StartAsync();
    public Task PauseCouchbaseAsync() => _couchbase.PauseAsync();
    public Task UnpauseCouchbaseAsync() => _couchbase.UnpauseAsync();
    public Task PauseRedisAsync() => _redis.PauseAsync();
    public Task UnpauseRedisAsync() => _redis.UnpauseAsync();
}

[CollectionDefinition(Name)]
public sealed class TelemetryCollection : ICollectionFixture<TelemetryEnvironment>
{
    public const string Name = "telemetry environment";
}
