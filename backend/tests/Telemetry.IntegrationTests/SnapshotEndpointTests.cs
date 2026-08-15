using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>
/// L2-006 — GET /api/telemetry/latest returns the newest stored point per known series,
/// derived from the time series documents themselves.
/// </summary>
[Collection(TelemetryCollection.Name)]
public sealed class SnapshotEndpointTests(TelemetryEnvironment environment)
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.Parse("2026-08-15T10:00:00Z");

    // L2-006 AC3: no telemetry ever stored yields 200 with an empty array.
    [Fact]
    public async Task Returns_empty_array_when_nothing_is_stored()
    {
        await environment.FlushTelemetryAsync();
        using var api = new ApiFactory(environment);
        using var http = api.CreateClient();

        var response = await http.GetAsync("/api/telemetry/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    // L2-006 AC1 and AC2: one entry per known series carrying that series' newest point,
    // reflecting newer values on subsequent calls.
    [Fact]
    public async Task Returns_newest_point_for_every_known_series()
    {
        await environment.FlushTelemetryAsync();
        var press1 = TelemetryEnvironment.Unique("press");
        var press2 = TelemetryEnvironment.Unique("press");
        await environment.SeedPointAsync(press1, "temperature", T0, 20.0);
        await environment.SeedPointAsync(press1, "temperature", T0.AddMinutes(5), 25.0);
        await environment.SeedPointAsync(press1, "pressure", T0.AddMinutes(1), 1.5);
        await environment.SeedPointAsync(press2, "temperature", T0.AddHours(2), 30.0); // separate hourly bucket

        using var api = new ApiFactory(environment);
        using var http = api.CreateClient();

        var body = await http.GetFromJsonAsync<JsonElement>("/api/telemetry/latest");

        Assert.Equal(3, body.GetArrayLength());
        var entries = body.EnumerateArray().ToDictionary(
            e => $"{e.GetProperty("deviceId").GetString()}/{e.GetProperty("metric").GetString()}");
        Assert.Equal(25.0, entries[$"{press1}/temperature"].GetProperty("value").GetDouble());
        Assert.Equal(T0.AddMinutes(5), entries[$"{press1}/temperature"].GetProperty("timestamp").GetDateTimeOffset());
        Assert.Equal(1.5, entries[$"{press1}/pressure"].GetProperty("value").GetDouble());
        Assert.Equal(30.0, entries[$"{press2}/temperature"].GetProperty("value").GetDouble());

        // AC2: a newer point moves the series' entry forward.
        await environment.SeedPointAsync(press1, "pressure", T0.AddMinutes(10), 2.5);
        var updated = await http.GetFromJsonAsync<JsonElement>("/api/telemetry/latest");
        var pressure = updated.EnumerateArray().Single(e =>
            e.GetProperty("deviceId").GetString() == press1 && e.GetProperty("metric").GetString() == "pressure");
        Assert.Equal(2.5, pressure.GetProperty("value").GetDouble());
        Assert.Equal(T0.AddMinutes(10), pressure.GetProperty("timestamp").GetDateTimeOffset());
    }
}
