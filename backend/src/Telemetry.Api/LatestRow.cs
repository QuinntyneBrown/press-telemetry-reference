namespace Telemetry.Api;

/// <summary>One row of the latest-values aggregate query result.</summary>
internal sealed record LatestRow(string deviceId, string metric, double[] latest);
