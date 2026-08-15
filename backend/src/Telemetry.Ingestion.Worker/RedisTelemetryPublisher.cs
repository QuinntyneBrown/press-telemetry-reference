using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Telemetry.Ingestion.Worker;

/// <summary>
/// Publishes each accepted point as camelCase JSON on the shared "telemetry" Redis channel,
/// through which every API instance broadcasts to its connected clients. A Redis outage is
/// logged and never blocks persistence; missed points are not replayed (L2-008).
/// </summary>
public sealed class RedisTelemetryPublisher(IOptions<WorkerOptions> options, ILogger<RedisTelemetryPublisher> logger) : IAsyncDisposable
{
    public const string ChannelName = "telemetry";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Lazy<ConnectionMultiplexer> _redis = new(() =>
    {
        var configuration = ConfigurationOptions.Parse(options.Value.RedisConnectionString);
        configuration.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(configuration);
    });

    public async Task PublishAsync(TelemetryPoint point, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(point, SerializerOptions);
            await _redis.Value.GetSubscriber()
                .PublishAsync(RedisChannel.Literal(ChannelName), payload)
                .WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Live fan-out to Redis failed for {DeviceId}/{Metric} ({Reason}); the point is not replayed",
                point.DeviceId, point.Metric, ex.Message);
        }
    }

    public ValueTask DisposeAsync()
        => _redis.IsValueCreated ? _redis.Value.DisposeAsync() : ValueTask.CompletedTask;
}
