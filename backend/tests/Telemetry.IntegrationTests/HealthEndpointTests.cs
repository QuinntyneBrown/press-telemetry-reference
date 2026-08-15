using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Telemetry.IntegrationTests.Infrastructure;

namespace Telemetry.IntegrationTests;

/// <summary>L2-021 AC1 — GET /health reports readiness of the Couchbase and Redis dependencies.</summary>
[Collection(TelemetryCollection.Name)]
public sealed class HealthEndpointTests(TelemetryEnvironment environment)
{
    [Fact]
    public async Task Health_returns_200_with_per_dependency_status_when_dependencies_are_reachable()
    {
        using var api = new ApiFactory(environment);
        using var http = api.CreateClient();

        var response = await http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Equal("Healthy", body.GetProperty("dependencies").GetProperty("couchbase").GetString());
        Assert.Equal("Healthy", body.GetProperty("dependencies").GetProperty("redis").GetString());
    }

    [Fact]
    public async Task Health_returns_503_naming_redis_when_redis_is_unreachable()
    {
        using var api = new ApiFactory(environment,
            settings => settings["Api:RedisConnectionString"] = "localhost:59999,connectTimeout=500");
        using var http = api.CreateClient();

        var response = await http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", body.GetProperty("dependencies").GetProperty("redis").GetString());
        Assert.Equal("Healthy", body.GetProperty("dependencies").GetProperty("couchbase").GetString());
    }

    [Fact]
    public async Task Health_returns_503_naming_couchbase_when_couchbase_is_unreachable()
    {
        using var api = new ApiFactory(environment,
            settings => settings["Api:CouchbaseConnectionString"] = "couchbase://localhost:59998");
        using var http = api.CreateClient();

        var response = await http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unhealthy", body.GetProperty("dependencies").GetProperty("couchbase").GetString());
        Assert.Equal("Healthy", body.GetProperty("dependencies").GetProperty("redis").GetString());
    }
}
