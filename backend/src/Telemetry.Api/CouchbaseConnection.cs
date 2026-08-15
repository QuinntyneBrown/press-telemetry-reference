using Couchbase;
using Microsoft.Extensions.Options;

namespace Telemetry.Api;

/// <summary>
/// Lazily connects to Couchbase and shares the cluster between the reader and the health
/// check. A failed connection attempt is retried on the next use so the API can start (and
/// report unhealthy) while Couchbase is down.
/// </summary>
public sealed class CouchbaseConnection(IOptions<ApiOptions> options) : IAsyncDisposable
{
    private readonly object _gate = new();
    private Task<ICluster>? _connect;

    public Task<ICluster> GetClusterAsync()
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
