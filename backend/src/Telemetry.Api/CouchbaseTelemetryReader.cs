using Couchbase;
using Couchbase.Query;
using Microsoft.Extensions.Options;

namespace Telemetry.Api;

/// <summary>
/// Read-only access to the hourly time series documents via Couchbase's _timeseries()
/// function (L2-005, L2-006). This component never writes telemetry.
/// </summary>
public sealed class CouchbaseTelemetryReader(CouchbaseConnection connection, IOptions<ApiOptions> options)
{
    // _timeseries() ranges are inclusive; the epoch-millisecond bound for "all data".
    private const long MaxTimestamp = 253402300799999;

    public async Task<IReadOnlyList<SeriesPointDto>> ReadRangeAsync(RangeQuery query, CancellationToken cancellationToken)
    {
        var cluster = await connection.GetClusterAsync();
        var statement = $$"""
            SELECT ts._t AS t, ts._v0 AS v
            FROM `{{options.Value.CouchbaseBucket}}` AS d
            UNNEST _timeseries(d, {"ts_ranges": [$from, $toInclusive]}) AS ts
            WHERE d.deviceId = $deviceId AND d.metric = $metric
              AND d.ts_start <= $toInclusive AND d.ts_end >= $from
            ORDER BY ts._t
            """;
        var result = await cluster.QueryAsync<RangeRow>(statement, queryOptions => queryOptions
            .Parameter("deviceId", query.DeviceId)
            .Parameter("metric", query.Metric)
            .Parameter("from", query.From.ToUnixTimeMilliseconds())
            .Parameter("toInclusive", query.To.ToUnixTimeMilliseconds() - 1)
            .ScanConsistency(QueryScanConsistency.RequestPlus)
            .CancellationToken(cancellationToken));
        return await result.Rows
            .Select(row => new SeriesPointDto(DateTimeOffset.FromUnixTimeMilliseconds(row.t), row.v))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TelemetryPointDto>> ReadLatestAsync(CancellationToken cancellationToken)
    {
        var cluster = await connection.GetClusterAsync();
        // An aggregate over the time series documents themselves; no latest-value record
        // or series registry exists anywhere in the solution (L2-006).
        var statement = $$"""
            SELECT d.deviceId, d.metric, MAX([p._t, p._v0]) AS latest
            FROM `{{options.Value.CouchbaseBucket}}` AS d
            UNNEST _timeseries(d, {"ts_ranges": [0, {{MaxTimestamp}}]}) AS p
            GROUP BY d.deviceId, d.metric
            """;
        var result = await cluster.QueryAsync<LatestRow>(statement, queryOptions => queryOptions
            .ScanConsistency(QueryScanConsistency.RequestPlus)
            .CancellationToken(cancellationToken));
        return await result.Rows
            .Select(row => new TelemetryPointDto(
                row.deviceId, row.metric, row.latest[1],
                DateTimeOffset.FromUnixTimeMilliseconds((long)row.latest[0])))
            .ToListAsync(cancellationToken);
    }

    private sealed record RangeRow(long t, double v);

    private sealed record LatestRow(string deviceId, string metric, double[] latest);
}
