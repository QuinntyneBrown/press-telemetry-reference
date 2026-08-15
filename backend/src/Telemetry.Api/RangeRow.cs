namespace Telemetry.Api;

/// <summary>One row of the _timeseries() range query result.</summary>
internal sealed record RangeRow(long t, double v);
