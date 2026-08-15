using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>
/// L2-005 — GET /api/telemetry/{deviceId}/{metric}?from&amp;to returns stored points in
/// [from, to) ascending; invalid input yields 400 ProblemDetails (with L2-019 AC1).
/// </summary>
[Collection(TelemetryCollection.Name)]
public sealed class RangeEndpointTests(TelemetryEnvironment environment) : IAsyncLifetime
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-08-15T10:00:00Z");

    private ApiFactory _api = null!;
    private HttpClient _http = null!;

    public Task InitializeAsync()
    {
        _api = new ApiFactory(environment);
        _http = _api.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        _api.Dispose();
        return Task.CompletedTask;
    }

    // L2-005 AC1: exactly the points in [from, to), ascending by timestamp.
    [Fact]
    public async Task Returns_points_in_half_open_range_ascending()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        await environment.SeedPointAsync(deviceId, "temperature", T0.AddMinutes(5), 5.0);
        await environment.SeedPointAsync(deviceId, "temperature", T0.AddMinutes(1), 1.0);
        await environment.SeedPointAsync(deviceId, "temperature", T0.AddMinutes(3), 3.0);
        await environment.SeedPointAsync(deviceId, "temperature", T0.AddMinutes(30), 30.0); // outside range
        await environment.SeedPointAsync(deviceId, "pressure", T0.AddMinutes(2), 99.0);     // other series

        var response = await _http.GetAsync(RangeUrl(deviceId, "temperature", T0.AddMinutes(1), T0.AddMinutes(30)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var points = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, points.GetArrayLength());
        Assert.Equal([1.0, 3.0, 5.0], points.EnumerateArray().Select(p => p.GetProperty("value").GetDouble()));
        Assert.Equal(T0.AddMinutes(1), points[0].GetProperty("timestamp").GetDateTimeOffset());
        // 'from' is inclusive (minute 1 returned above); 'to' is exclusive.
        var toExclusive = await _http.GetFromJsonAsync<JsonElement>(RangeUrl(deviceId, "temperature", T0, T0.AddMinutes(5)));
        Assert.Equal(2, toExclusive.GetArrayLength());
    }

    // L2-003 AC3 (read side): points spanning two hourly bucket documents are both returned.
    [Fact]
    public async Task Returns_points_from_both_hourly_buckets_when_range_spans_them()
    {
        var deviceId = TelemetryEnvironment.Unique("press");
        await environment.SeedPointAsync(deviceId, "temperature", T0.AddMinutes(59), 59.0);
        await environment.SeedPointAsync(deviceId, "temperature", T0.AddMinutes(61), 61.0);

        var response = await _http.GetFromJsonAsync<JsonElement>(
            RangeUrl(deviceId, "temperature", T0.AddMinutes(30), T0.AddMinutes(90)));

        Assert.Equal(2, response.GetArrayLength());
        Assert.Equal([59.0, 61.0], response.EnumerateArray().Select(p => p.GetProperty("value").GetDouble()));
    }

    // L2-005 AC2: empty series or range returns 200 with an empty array.
    [Fact]
    public async Task Returns_empty_array_when_no_data_exists()
    {
        var response = await _http.GetAsync(RangeUrl(TelemetryEnvironment.Unique("silent"), "temperature", T0, T0.AddHours(1)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var points = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, points.GetArrayLength());
    }

    // L2-005 AC3: missing or malformed from/to yields 400 ProblemDetails naming the parameter.
    [Theory]
    [InlineData("?to=2026-08-15T11:00:00Z", "from")]
    [InlineData("?from=2026-08-15T10:00:00Z", "to")]
    [InlineData("?from=not-a-date&to=2026-08-15T11:00:00Z", "from")]
    [InlineData("?from=2026-08-15T10:00:00Z&to=not-a-date", "to")]
    public async Task Rejects_missing_or_malformed_range_parameters(string query, string parameter)
    {
        var response = await _http.GetAsync($"/api/telemetry/press-01/temperature{query}");

        await AssertProblemAsync(response, parameter);
    }

    // L2-005 AC4: from >= to yields 400 ProblemDetails naming both parameters.
    [Fact]
    public async Task Rejects_inverted_range()
    {
        var response = await _http.GetAsync(RangeUrl("press-01", "temperature", T0.AddHours(1), T0));

        var problem = await AssertProblemAsync(response, "from");
        Assert.Contains("to", problem);
    }

    // L2-005 AC5: ranges wider than 24 hours are rejected.
    [Fact]
    public async Task Rejects_range_wider_than_24_hours()
    {
        var response = await _http.GetAsync(RangeUrl("press-01", "temperature", T0, T0.AddHours(25)));

        await AssertProblemAsync(response, "24");
    }

    // L2-019 AC1: identifiers violating the 1-64 char [A-Za-z0-9_-]+ rule yield 400.
    [Theory]
    [InlineData("bad%20device", "temperature")]
    [InlineData("press-01", "bad.metric")]
    public async Task Rejects_invalid_identifiers(string deviceId, string metric)
    {
        var response = await _http.GetAsync(RangeUrl(deviceId, metric, T0, T0.AddHours(1)));

        await AssertProblemAsync(response);
    }

    [Fact]
    public async Task Rejects_identifiers_longer_than_64_characters()
    {
        var response = await _http.GetAsync(RangeUrl(new string('a', 65), "temperature", T0, T0.AddHours(1)));

        await AssertProblemAsync(response);
    }

    private static string RangeUrl(string deviceId, string metric, DateTimeOffset from, DateTimeOffset to)
        => $"/api/telemetry/{deviceId}/{metric}?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

    private static async Task<string> AssertProblemAsync(HttpResponseMessage response, string? expectedText = null)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        if (expectedText is not null)
        {
            Assert.Contains(expectedText, body, StringComparison.OrdinalIgnoreCase);
        }

        return body;
    }
}
