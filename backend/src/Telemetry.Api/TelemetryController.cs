using Microsoft.AspNetCore.Mvc;

namespace Telemetry.Api;

/// <summary>Historical range and first-render snapshot endpoints (L2-005, L2-006).</summary>
[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController(TelemetryQueryValidator validator, CouchbaseTelemetryReader reader) : ControllerBase
{
    [HttpGet("latest")]
    public async Task<ActionResult<IReadOnlyList<TelemetryPointDto>>> GetLatest(CancellationToken cancellationToken)
        => Ok(await reader.ReadLatestAsync(cancellationToken));

    [HttpGet("{deviceId}/{metric}")]
    public async Task<ActionResult<IReadOnlyList<SeriesPointDto>>> GetRange(
        string deviceId, string metric, [FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken)
    {
        var validation = validator.ValidateRange(deviceId, metric, from, to);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.Errors));
        }

        var query = new RangeQuery(deviceId, metric, validation.From, validation.To);
        return Ok(await reader.ReadRangeAsync(query, cancellationToken));
    }
}
