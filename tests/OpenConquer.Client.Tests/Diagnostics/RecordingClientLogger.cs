using Microsoft.Extensions.Logging;

namespace OpenConquer.Client.Tests.Diagnostics;

internal sealed class RecordingClientLogger : ILogger
{
    internal List<RecordedEvent> Events { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        KeyValuePair<string, object?>[] fields =
            ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToArray();
        Events.Add(new RecordedEvent(eventId.Id, logLevel, formatter(state, exception), fields, exception));
    }

    internal sealed record RecordedEvent(int Id, LogLevel Level, string Message,
        IReadOnlyList<KeyValuePair<string, object?>> Fields, Exception? Exception);
}
