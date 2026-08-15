using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>L2-019 AC2 (CORS allowlist) and L2-018 AC3 (fail fast on missing configuration).</summary>
[Collection(TelemetryCollection.Name)]
public sealed class ApiHardeningTests(TelemetryEnvironment environment)
{
    [Fact]
    public async Task Allows_cross_origin_requests_only_from_configured_origins()
    {
        using var api = new ApiFactory(environment);
        using var http = api.CreateClient();

        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/telemetry/latest");
        allowed.Headers.Add("Origin", "http://dashboard.test");
        var allowedResponse = await http.SendAsync(allowed);
        Assert.Equal("http://dashboard.test", Assert.Single(
            allowedResponse.Headers.GetValues("Access-Control-Allow-Origin")));

        using var denied = new HttpRequestMessage(HttpMethod.Get, "/api/telemetry/latest");
        denied.Headers.Add("Origin", "http://evil.test");
        var deniedResponse = await http.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Fails_fast_naming_the_missing_setting_when_configuration_is_absent()
    {
        await using var api = new ApiFactory(environment,
            settings => settings["Api:CouchbaseConnectionString"] = "");

        var startup = Record.Exception(() => api.CreateClient());

        Assert.NotNull(startup);
        Assert.Contains("Api:CouchbaseConnectionString", startup.ToString());
    }
}
