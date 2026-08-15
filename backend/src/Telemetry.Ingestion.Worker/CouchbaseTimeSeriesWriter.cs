using Couchbase;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Telemetry.Ingestion.Worker;

/// <summary>
/// The solution's sole telemetry writer. Appends each valid point to its hourly
/// ts_start/ts_end/ts_data document with a single sub-document upsert, retrying up to 3
/// attempts with backoff before dropping the point with an error log (L2-003, L2-004).
/// </summary>
public sealed class CouchbaseTimeSeriesWriter(IOptions<WorkerOptions> options, ILogger<CouchbaseTimeSeriesWriter> logger) : IAsyncDisposable
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    private readonly object _gate = new();
    private Task<ICluster>? _connect;

    public async Task AppendAsync(TelemetryPoint point, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await UpsertBucketAsync(TimeSeriesDocumentKey.For(point), point, cancellationToken);
                logger.LogDebug("Stored telemetry point {DeviceId}/{Metric} at {Timestamp}",
                    point.DeviceId, point.Metric, point.Timestamp);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < RetryDelays.Length)
            {
                logger.LogWarning("Couchbase write failed on attempt {Attempt} of 3 ({Reason}); retrying in {Delay}",
                    attempt + 1, ex.Message, RetryDelays[attempt]);
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dropping telemetry point after 3 failed write attempts: {DeviceId}/{Metric} at {Timestamp}",
                    point.DeviceId, point.Metric, point.Timestamp);
                return;
            }
        }
    }

    private async Task UpsertBucketAsync(TimeSeriesDocumentKey key, TelemetryPoint point, CancellationToken cancellationToken)
    {
        var cluster = await GetClusterAsync();
        var bucket = await cluster.BucketAsync(options.Value.CouchbaseBucket);
        await bucket.DefaultCollection().MutateInAsync(key.ToString(), specs =>
            {
                specs.Upsert("deviceId", key.DeviceId);
                specs.Upsert("metric", key.Metric);
                specs.Upsert("ts_start", key.HourStart.ToUnixTimeMilliseconds());
                specs.Upsert("ts_end", key.HourEnd.ToUnixTimeMilliseconds() - 1);
                specs.ArrayAppend("ts_data",
                    new[] { new object[] { point.Timestamp.ToUnixTimeMilliseconds(), point.Value } }, true);
            },
            mutateOptions => mutateOptions
                .StoreSemantics(StoreSemantics.Upsert)
                .CancellationToken(cancellationToken));
    }

    private Task<ICluster> GetClusterAsync()
    {
        lock (_gate)
        {
            if (_connect is null || _connect.IsFaulted || _connect.IsCanceled)
            {
                _connect = ConnectAsync();
            }

            return _connect;
        }
    }

    private async Task<ICluster> ConnectAsync()
    {
        var settings = options.Value;
        var cluster = await Cluster.ConnectAsync(
            settings.CouchbaseConnectionString, settings.CouchbaseUsername, settings.CouchbasePassword);
        var bucket = await cluster.BucketAsync(settings.CouchbaseBucket);
        await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
        logger.LogInformation("Connected to Couchbase at {ConnectionString}", settings.CouchbaseConnectionString);
        return cluster;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connect is { IsCompletedSuccessfully: true })
        {
            await (await _connect).DisposeAsync();
        }
    }
}
