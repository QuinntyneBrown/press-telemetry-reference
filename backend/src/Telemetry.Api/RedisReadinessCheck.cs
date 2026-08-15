using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Telemetry.Api;

/// <summary>Readiness of the Redis dependency, aggregated into GET /health (L2-021).</summary>
public sealed class RedisReadinessCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis is unreachable.", ex);
        }
    }
}
