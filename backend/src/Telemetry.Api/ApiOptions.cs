namespace Telemetry.Api;

/// <summary>Validated configuration for the API's Couchbase, Redis, and CORS settings (L2-018).</summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    public string CouchbaseConnectionString { get; set; } = "";
    public string CouchbaseUsername { get; set; } = "";
    public string CouchbasePassword { get; set; } = "";
    public string CouchbaseBucket { get; set; } = "";
    public string RedisConnectionString { get; set; } = "";
    public string[] CorsOrigins { get; set; } = [];
}
