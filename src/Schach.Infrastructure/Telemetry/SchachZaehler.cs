using System.Diagnostics.Metrics;

namespace Schach.Infrastructure.Telemetry;

/// <summary>
/// Zaehler am Meter <see cref="SchachMetrics.MeterName"/> (SCHACH-SPEC 14.2), Singleton. S2b legt
/// <c>schach.limits.abgelehnt{policy}</c> und <c>schach.engine.fehler{art}</c> an. Tags tragen nur
/// Policy- oder Artnamen, nie eine IP, Partie-Id oder E-Mail-Adresse.
/// </summary>
public sealed class SchachZaehler
{
    private readonly Counter<long> _limitsAbgelehnt;
    private readonly Counter<long> _engineFehler;

    public SchachZaehler(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(SchachMetrics.MeterName);
        _limitsAbgelehnt = meter.CreateCounter<long>(SchachMetrics.LimitsAbgelehnt, description: "Abgelehnte Anfragen je Limit-Policy");
        _engineFehler = meter.CreateCounter<long>(SchachMetrics.EngineFehler, description: "Fehler und Ersatz-Engine-Aufrufe des Engine-Clients");
    }

    /// <summary>Eine Anfrage wurde von der genannten Policy abgelehnt.</summary>
    public void LimitAbgelehnt(string policy) =>
        _limitsAbgelehnt.Add(1, new KeyValuePair<string, object?>("policy", policy));

    /// <summary>Ein Engine-Aufruf schlug fehl oder wich auf Fairy-Stockfish aus.</summary>
    public void EngineFehler(string art) =>
        _engineFehler.Add(1, new KeyValuePair<string, object?>("art", art));
}
