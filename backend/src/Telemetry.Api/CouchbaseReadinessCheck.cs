using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Telemetry.Api;

/// <summary>Readiness of the Couchbase dependency, aggregated into GET /health (L2-021).</summary>
public sealed class CouchbaseReadinessCheck(CouchbaseConnection connection, IOptions<ApiOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var cluster = await connection.GetClusterAsync().WaitAsync(cancellationToken);
            var bucket = await cluster.BucketAsync(options.Value.CouchbaseBucket).AsTask().WaitAsync(cancellationToken);
            await bucket.DefaultCollection().ExistsAsync("readiness-probe").WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Couchbase is unreachable.", ex);
        }
    }
}
