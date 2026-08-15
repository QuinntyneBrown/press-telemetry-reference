using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telemetry.Api;

namespace Telemetry.IntegrationTests.Infrastructure;

/// <summary>An API instance under test, wired to the shared container environment by default.</summary>
public sealed class ApiFactory : WebApplicationFactory<ApiMarker>
{
    private readonly Dictionary<string, string?> _settings;

    public TestLogCollector Logs { get; } = new();

    public ApiFactory(TelemetryEnvironment environment, Action<Dictionary<string, string?>>? configure = null)
    {
        _settings = new Dictionary<string, string?>
        {
            ["Api:CouchbaseConnectionString"] = environment.CouchbaseConnectionString,
            ["Api:CouchbaseUsername"] = TelemetryEnvironment.CouchbaseUsername,
            ["Api:CouchbasePassword"] = TelemetryEnvironment.CouchbasePassword,
            ["Api:CouchbaseBucket"] = TelemetryEnvironment.CouchbaseBucket,
            ["Api:RedisConnectionString"] = environment.RedisConnectionString,
            ["Api:CorsOrigins:0"] = "http://dashboard.test",
        };
        configure?.Invoke(_settings);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }
}
