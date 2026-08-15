using Microsoft.Extensions.Options;

namespace Telemetry.Ingestion.Worker;

/// <summary>Fails startup with the names of any missing required settings (L2-018 AC3).</summary>
public sealed class WorkerOptionsValidator : IValidateOptions<WorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkerOptions options)
    {
        var missing = new List<string>();
        Require(missing, options.MqttBroker, nameof(options.MqttBroker));
        Require(missing, options.MqttTopicFilter, nameof(options.MqttTopicFilter));
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
            missing.Add($"{WorkerOptions.SectionName}:{property}");
        }
    }
}
