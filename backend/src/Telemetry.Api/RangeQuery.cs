namespace Telemetry.Api;

/// <summary>A validated historical range query.</summary>
public sealed record RangeQuery(string DeviceId, string Metric, DateTimeOffset From, DateTimeOffset To);
