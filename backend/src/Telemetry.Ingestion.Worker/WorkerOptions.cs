namespace Telemetry.Ingestion.Worker;

/// <summary>Validated configuration for the worker's MQTT, Couchbase, and Redis settings (L2-018).</summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public string MqttBroker { get; set; } = "";
    public string MqttTopicFilter { get; set; } = "telemetry/#";
    public string CouchbaseConnectionString { get; set; } = "";
    public string CouchbaseUsername { get; set; } = "";
    public string CouchbasePassword { get; set; } = "";
    public string CouchbaseBucket { get; set; } = "";
    public string RedisConnectionString { get; set; } = "";
}
