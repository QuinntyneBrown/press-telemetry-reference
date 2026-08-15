using Microsoft.Extensions.Logging;

namespace Telemetry.IntegrationTests.Infrastructure;

public sealed record LogEntry(LogLevel Level, string Category, string Message);
