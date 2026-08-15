namespace Telemetry.Api;

/// <summary>The outcome of validating a historical range query.</summary>
public sealed class RangeValidation
{
    public Dictionary<string, string[]> Errors { get; } = [];
    public DateTimeOffset From { get; set; }
    public DateTimeOffset To { get; set; }
    public bool IsValid => Errors.Count == 0;
}
