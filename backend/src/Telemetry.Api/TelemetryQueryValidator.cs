using System.Globalization;
using System.Text.RegularExpressions;

namespace Telemetry.Api;

/// <summary>Boundary validation for identifiers and time ranges (L2-005, L2-019).</summary>
public sealed partial class TelemetryQueryValidator
{
    public static readonly TimeSpan MaxWindow = TimeSpan.FromHours(24);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdentifierPattern();

    public RangeValidation ValidateRange(string deviceId, string metric, string? from, string? to)
    {
        var validation = new RangeValidation();
        ValidateSeries(deviceId, metric, validation.Errors);

        var hasFrom = TryParseTimestamp(from, "from", validation.Errors, out var fromValue);
        var hasTo = TryParseTimestamp(to, "to", validation.Errors, out var toValue);
        if (hasFrom && hasTo)
        {
            if (fromValue >= toValue)
            {
                validation.Errors["range"] = ["'from' must be earlier than 'to'."];
            }
            else if (toValue - fromValue > MaxWindow)
            {
                validation.Errors["range"] = ["The requested range exceeds the 24-hour maximum window."];
            }
        }

        validation.From = fromValue;
        validation.To = toValue;
        return validation;
    }

    public void ValidateSeries(string deviceId, string metric, Dictionary<string, string[]> errors)
    {
        if (!IdentifierPattern().IsMatch(deviceId))
        {
            errors["deviceId"] = ["'deviceId' must be 1-64 characters matching [A-Za-z0-9_-]."];
        }

        if (!IdentifierPattern().IsMatch(metric))
        {
            errors["metric"] = ["'metric' must be 1-64 characters matching [A-Za-z0-9_-]."];
        }
    }

    private static bool TryParseTimestamp(string? value, string parameter, Dictionary<string, string[]> errors, out DateTimeOffset parsed)
    {
        if (!string.IsNullOrWhiteSpace(value) && DateTimeOffset.TryParse(
                value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
        {
            return true;
        }

        errors[parameter] = [$"'{parameter}' is required and must be an ISO-8601 timestamp."];
        parsed = default;
        return false;
    }
}

public sealed class RangeValidation
{
    public Dictionary<string, string[]> Errors { get; } = [];
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public bool IsValid => Errors.Count == 0;
}
