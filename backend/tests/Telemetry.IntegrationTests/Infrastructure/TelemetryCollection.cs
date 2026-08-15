namespace Telemetry.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class TelemetryCollection : ICollectionFixture<TelemetryEnvironment>
{
    public const string Name = "telemetry environment";
}
