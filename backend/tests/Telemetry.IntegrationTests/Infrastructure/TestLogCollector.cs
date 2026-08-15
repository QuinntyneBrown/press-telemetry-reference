using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Telemetry.IntegrationTests.Infrastructure;

public sealed record LogEntry(LogLevel Level, string Category, string Message);

/// <summary>Captures log entries so acceptance tests can assert on structured logging behaviour.</summary>
public sealed class TestLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CollectorLogger(_entries, categoryName);

    public void Dispose()
    {
    }

    private sealed class CollectorLogger(ConcurrentQueue<LogEntry> entries, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Enqueue(new LogEntry(logLevel, category, formatter(state, exception)));
    }
}
