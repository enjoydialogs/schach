using System.Diagnostics.Metrics;

namespace Schach.Infrastructure.Telemetry;

/// <summary>
/// Zaehler fuer Limits und Engine (S2b), Singleton. Duenne Fassade ueber <see cref="SchachMeter"/>
/// (S2d), damit jedes Instrument genau einmal angelegt wird, Tagwerte nie eine IP oder E-Mail-Adresse
/// tragen und die Zahlen in die Tageszeile gehen.
/// </summary>
public sealed class SchachZaehler(IMeterFactory meterFactory)
{
    private readonly SchachMeter _meter = new(meterFactory);

    /// <summary>Eine Anfrage wurde von der genannten Policy abgelehnt.</summary>
    public void LimitAbgelehnt(string policy) => _meter.LimitAbgelehnt(policy);

    /// <summary>Ein Engine-Aufruf schlug fehl oder wich auf Fairy-Stockfish aus.</summary>
    public void EngineFehler(string art) => _meter.EngineFehler(art);
}
