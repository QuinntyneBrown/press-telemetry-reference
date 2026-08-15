namespace Telemetry.Ingestion.Worker;

/// <summary>A validated telemetry measurement: device, metric, finite value, UTC timestamp.</summary>
public sealed record TelemetryPoint(string DeviceId, string Metric, double Value, DateTimeOffset Timestamp);
