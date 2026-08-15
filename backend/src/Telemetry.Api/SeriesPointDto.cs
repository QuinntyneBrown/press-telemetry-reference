namespace Telemetry.Api;

/// <summary>One point of a single series, as returned by the range endpoint (L2-005).</summary>
public sealed record SeriesPointDto(DateTimeOffset Timestamp, double Value);
