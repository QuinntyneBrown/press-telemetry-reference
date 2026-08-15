using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Telemetry.Api;

/// <summary>
/// Broadcast hub at /hubs/telemetry. Clients receive every ingested point as a "telemetry"
/// message; there are no subscription operations or server-side series groups (L2-007).
/// </summary>
public sealed class TelemetryHub : Hub;

/// <summary>
/// Routes each worker-published point from the shared "telemetry" Redis channel to every
/// client connected to this API instance. The live path never reads Couchbase; a Redis outage
/// pauses delivery until the subscription reconnects (L2-008).
/// </summary>
public sealed class RedisLiveStream(
    IConnectionMultiplexer redis,
    IHubContext<TelemetryHub> hub,
    ILogger<RedisLiveStream> logger) : BackgroundService
{
    public const string ChannelName = "telemetry";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queue = await redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(ChannelName));
        queue.OnMessage(message => DeliverAsync(message.Message, stoppingToken));
        logger.LogInformation("Subscribed to Redis channel {Channel} for live telemetry", ChannelName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }

        await queue.UnsubscribeAsync();
    }

    private async Task DeliverAsync(RedisValue payload, CancellationToken cancellationToken)
    {
        try
        {
            var point = JsonSerializer.Deserialize<TelemetryPointDto>((string)payload!, SerializerOptions);
            if (point is not null)
            {
                await hub.Clients.All.SendAsync("telemetry", point, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to broadcast a live telemetry message ({Reason})", ex.Message);
        }
    }
}
