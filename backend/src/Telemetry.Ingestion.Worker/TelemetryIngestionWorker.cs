using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Telemetry.Ingestion.Worker;

/// <summary>
/// Coordinates the ingestion pipeline: validated points fan out to persistence and live
/// publication through independent queues, so a stall or failure in either destination never
/// delays the other or later messages (L2-002, L2-004, L2-008).
/// </summary>
public sealed class TelemetryIngestionWorker(
    MqttTelemetrySource source,
    TelemetryPointParser parser,
    CouchbaseTimeSeriesWriter writer,
    RedisTelemetryPublisher livePublisher,
    ILogger<TelemetryIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var persistQueue = CreateQueue();
        var liveQueue = CreateQueue();

        await Task.WhenAll(
            source.RunAsync(stoppingToken),
            PumpAsync(persistQueue.Writer, liveQueue.Writer, stoppingToken),
            ConsumeAsync(persistQueue.Reader, writer.AppendAsync, stoppingToken),
            ConsumeAsync(liveQueue.Reader, livePublisher.PublishAsync, stoppingToken));
    }

    private static Channel<TelemetryPoint> CreateQueue()
        => Channel.CreateBounded<TelemetryPoint>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });

    private async Task PumpAsync(
        ChannelWriter<TelemetryPoint> persist, ChannelWriter<TelemetryPoint> live, CancellationToken cancellationToken)
    {
        await foreach (var payload in source.Messages(cancellationToken))
        {
            var result = parser.Parse(payload);
            if (result.Point is not { } point)
            {
                logger.LogWarning("Dropped invalid telemetry message: {Reason}", result.Error);
                continue;
            }

            logger.LogDebug("Accepted telemetry point {DeviceId}/{Metric} at {Timestamp}",
                point.DeviceId, point.Metric, point.Timestamp);
            persist.TryWrite(point);
            live.TryWrite(point);
        }
    }

    private static async Task ConsumeAsync(
        ChannelReader<TelemetryPoint> reader, Func<TelemetryPoint, CancellationToken, Task> handle, CancellationToken cancellationToken)
    {
        await foreach (var point in reader.ReadAllAsync(cancellationToken))
        {
            await handle(point, cancellationToken);
        }
    }
}
