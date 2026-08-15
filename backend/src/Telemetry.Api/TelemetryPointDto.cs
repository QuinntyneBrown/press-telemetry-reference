namespace Telemetry.Api;

/// <summary>A fully identified point, as returned by the snapshot endpoint and the live hub.</summary>
public sealed record TelemetryPointDto(string DeviceId, string Metric, double Value, DateTimeOffset Timestamp);
