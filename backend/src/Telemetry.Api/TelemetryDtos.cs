namespace Telemetry.Api;

/// <summary>One point of a single series, as returned by the range endpoint (L2-005).</summary>
public sealed record SeriesPointDto(DateTimeOffset Timestamp, double Value);

/// <summary>A fully identified point, as returned by the snapshot endpoint and the live hub.</summary>
public sealed record TelemetryPointDto(string DeviceId, string Metric, double Value, DateTimeOffset Timestamp);

/// <summary>A validated historical range query.</summary>
public sealed record RangeQuery(string DeviceId, string Metric, DateTimeOffset From, DateTimeOffset To);
