using Microsoft.Extensions.Options;

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

/// <summary>Fails startup with the names of any missing required settings (L2-018 AC3).</summary>
public sealed class ApiOptionsValidator : IValidateOptions<ApiOptions>
{
    public ValidateOptionsResult Validate(string? name, ApiOptions options)
    {
        var missing = new List<string>();
        Require(missing, options.CouchbaseConnectionString, nameof(options.CouchbaseConnectionString));
        Require(missing, options.CouchbaseUsername, nameof(options.CouchbaseUsername));
        Require(missing, options.CouchbasePassword, nameof(options.CouchbasePassword));
        Require(missing, options.CouchbaseBucket, nameof(options.CouchbaseBucket));
        Require(missing, options.RedisConnectionString, nameof(options.RedisConnectionString));
        return missing.Count > 0
            ? ValidateOptionsResult.Fail($"Missing required configuration: {string.Join(", ", missing)}")
            : ValidateOptionsResult.Success;
    }

    private static void Require(List<string> missing, string value, string property)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add($"{ApiOptions.SectionName}:{property}");
        }
    }
}
