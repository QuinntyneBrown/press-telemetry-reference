using Microsoft.AspNetCore.SignalR.Client;

namespace Telemetry.IntegrationTests;

internal sealed record LiveClient(HubConnection Connection) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
