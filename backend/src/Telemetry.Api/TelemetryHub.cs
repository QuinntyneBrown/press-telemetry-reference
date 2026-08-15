using Microsoft.AspNetCore.SignalR;

namespace Telemetry.Api;

/// <summary>
/// Broadcast hub at /hubs/telemetry. Clients receive every ingested point as a "telemetry"
/// message; there are no subscription operations or server-side series groups (L2-007).
/// </summary>
public sealed class TelemetryHub : Hub;
