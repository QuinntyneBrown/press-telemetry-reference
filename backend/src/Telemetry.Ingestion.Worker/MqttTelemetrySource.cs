using System.Buffers;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;

namespace Telemetry.Ingestion.Worker;

/// <summary>
/// Owns the MQTT connection: subscribes at QoS 0 and reconnects forever with exponential
/// backoff from 1 s to a 30 s cap. Broker unavailability never terminates the worker (L2-001).
/// </summary>
public sealed class MqttTelemetrySource(IOptions<WorkerOptions> options, ILogger<MqttTelemetrySource> logger) : IDisposable
{
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly Channel<ReadOnlyMemory<byte>> _messages = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private readonly IMqttClient _client = new MqttClientFactory().CreateMqttClient();

    public IAsyncEnumerable<ReadOnlyMemory<byte>> Messages(CancellationToken cancellationToken)
        => _messages.Reader.ReadAllAsync(cancellationToken);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var broker = new Uri(settings.MqttBroker);
        var clientOptions = new MqttClientOptionsBuilder().WithTcpServer(broker.Host, broker.Port).Build();

        var disconnected = new SemaphoreSlim(0);
        _client.ApplicationMessageReceivedAsync += eventArgs =>
        {
            _messages.Writer.TryWrite(eventArgs.ApplicationMessage.Payload.ToArray());
            return Task.CompletedTask;
        };
        _client.DisconnectedAsync += _ =>
        {
            disconnected.Release();
            return Task.CompletedTask;
        };

        var delay = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                while (disconnected.CurrentCount > 0)
                {
                    disconnected.Wait(0, CancellationToken.None);
                }

                await _client.ConnectAsync(clientOptions, cancellationToken);
                logger.LogInformation("Connected to MQTT broker {BrokerAddress}", settings.MqttBroker);
                await _client.SubscribeAsync(settings.MqttTopicFilter, MqttQualityOfServiceLevel.AtMostOnce, cancellationToken);
                logger.LogInformation("Subscribed to topic filter {TopicFilter} at QoS 0 on {BrokerAddress}",
                    settings.MqttTopicFilter, settings.MqttBroker);
                delay = TimeSpan.FromSeconds(1);

                await disconnected.WaitAsync(cancellationToken);
                logger.LogWarning("Connection to MQTT broker {BrokerAddress} lost; reconnecting in {Delay}",
                    settings.MqttBroker, delay);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Connecting to MQTT broker {BrokerAddress} failed ({Reason}); retrying in {Delay}",
                    settings.MqttBroker, ex.Message, delay);
            }

            try
            {
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRetryDelay.Ticks));
        }
    }

    public void Dispose() => _client.Dispose();
}
