using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Telemetry.IntegrationTests.Infrastructure;

/// <summary>Captures log entries so acceptance tests can assert on structured logging behaviour.</summary>
public sealed class TestLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CollectorLogger(_entries, categoryName);

    public void Dispose()
    {
    }
}
