using Microsoft.Extensions.Logging;

namespace Schach.Infrastructure.Tests.Betrieb;

/// <summary>Faengt Logzeilen fuer Assertions ein (Ebene, EventName, fertiger Text).</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string? EventName, string Text)> _zeilen = [];

    public IReadOnlyList<(LogLevel Level, string? EventName, string Text)> Zeilen
    {
        get
        {
            lock (_zeilen)
            {
                return [.. _zeilen];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_zeilen)
        {
            _zeilen.Add((logLevel, eventId.Name, formatter(state, exception)));
        }
    }
}
