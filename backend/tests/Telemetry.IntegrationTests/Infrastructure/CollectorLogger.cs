using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Telemetry.IntegrationTests.Infrastructure;

internal sealed class CollectorLogger(ConcurrentQueue<LogEntry> entries, string category) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => entries.Enqueue(new LogEntry(logLevel, category,
            exception is null ? formatter(state, exception) : $"{formatter(state, exception)} | {exception}"));
}
