using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Telemetry.Ingestion.Worker;

public readonly record struct ParseResult(TelemetryPoint? Point, string? Error)
{
    public static ParseResult Valid(TelemetryPoint point) => new(point, null);
    public static ParseResult Invalid(string error) => new(null, error);
}

/// <summary>Parses and validates MQTT payloads against the telemetry point shape (L2-002).</summary>
public sealed partial class TelemetryPointParser
{
    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdentifierPattern();

    public ParseResult Parse(ReadOnlyMemory<byte> payload)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return ParseResult.Invalid("the payload is not valid JSON");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ParseResult.Invalid("the payload is not a JSON object");
            }

            if (!TryGetIdentifier(root, "deviceId", out var deviceId, out var error)
                || !TryGetIdentifier(root, "metric", out var metric, out error))
            {
                return ParseResult.Invalid(error);
            }

            if (!root.TryGetProperty("value", out var valueElement)
                || valueElement.ValueKind != JsonValueKind.Number
                || !valueElement.TryGetDouble(out var value)
                || !double.IsFinite(value))
            {
                return ParseResult.Invalid("'value' is missing or not a finite number");
            }

            if (!root.TryGetProperty("timestamp", out var timestampElement)
                || timestampElement.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(timestampElement.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var timestamp))
            {
                return ParseResult.Invalid("'timestamp' is missing or not an ISO-8601 timestamp");
            }

            return ParseResult.Valid(new TelemetryPoint(deviceId, metric, value, timestamp));
        }
    }

    private static bool TryGetIdentifier(JsonElement root, string property, out string value, out string error)
    {
        value = "";
        error = "";
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.String
            || !IdentifierPattern().IsMatch(value = element.GetString()!))
        {
            error = $"'{property}' is missing or violates the 1-64 character [A-Za-z0-9_-] rule";
            return false;
        }

        return true;
    }
}
