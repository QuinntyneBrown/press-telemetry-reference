namespace Telemetry.Ingestion.Worker;

/// <summary>Identifies one hourly time series document for a series (L2-003).</summary>
public sealed record TimeSeriesDocumentKey(string DeviceId, string Metric, DateTimeOffset HourStart)
{
    public static TimeSeriesDocumentKey For(TelemetryPoint point)
    {
        var utc = point.Timestamp.UtcDateTime;
        return new TimeSeriesDocumentKey(point.DeviceId, point.Metric,
            new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero));
    }

    public DateTimeOffset HourEnd => HourStart.AddHours(1);

    public override string ToString() => $"{DeviceId}::{Metric}::{HourStart:yyyy-MM-ddTHH}";
}
