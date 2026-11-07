namespace Schach.Application.Abstractions;

/// <summary>
/// Serverzeit fuer Uhr, Sweep und Zeitstempel (SCHACH-SPEC 8.6). Umgesetzt ueber
/// <see cref="TimeProvider"/>; Tests setzen einen <c>FakeTimeProvider</c> ein.
/// Alle Zeitpunkte sind UTC.
/// </summary>
public interface IGameClock
{
    DateTimeOffset UtcNow { get; }
}
