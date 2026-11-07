using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Schach.Infrastructure.Engine;

/// <summary>Schluessel der Ueberlastsperre im Abschnitt <c>Engine</c> (SCHACH-SPEC 11.2).</summary>
public sealed class EngineUeberlastOptions
{
    /// <summary>So viele Ausfaelle in 30 s gelten als Ueberlast.</summary>
    public int UeberlastFehlerIn30s { get; set; } = 10;

    /// <summary>So lange werden danach keine neuen Gast-Partien angenommen.</summary>
    public int UeberlastSperreSekunden { get; set; } = 60;
}

/// <summary>
/// Erkennt Engine-Ueberlast (SCHACH-SPEC 11.2, Abbau unter Last Schritt 1): der Engine-Client meldet
/// jeden Ausfall (503, Zeitueberschreitung, keine Verbindung). Erreichen die Ausfaelle der letzten
/// 30 s die Schwelle, ist fuer <see cref="EngineUeberlastOptions.UeberlastSperreSekunden"/> gesperrt.
/// Die Warn-Logzeile <c>EngineUeberlast</c> erscheint einmal je Sperre (SCHACH-SPEC 14.2).
/// Singleton, nur im Speicher.
/// </summary>
public sealed partial class EngineUeberlastWaechter(
    IOptions<EngineUeberlastOptions> options,
    TimeProvider zeit,
    ILogger<EngineUeberlastWaechter> logger)
{
    private static readonly TimeSpan Fenster = TimeSpan.FromSeconds(30);

    private readonly EngineUeberlastOptions _options = options.Value;
    private readonly Queue<DateTimeOffset> _ausfaelle = new();
    private readonly Lock _lock = new();
    private DateTimeOffset _gesperrtBis = DateTimeOffset.MinValue;

    /// <summary>Wahr, solange keine neuen Gast-Partien starten duerfen.</summary>
    public bool IstGesperrt
    {
        get
        {
            lock (_lock)
            {
                return zeit.GetUtcNow() < _gesperrtBis;
            }
        }
    }

    public void AusfallMelden()
    {
        var jetzt = zeit.GetUtcNow();
        bool neueSperre;
        lock (_lock)
        {
            _ausfaelle.Enqueue(jetzt);
            while (_ausfaelle.Count > 0 && _ausfaelle.Peek() <= jetzt - Fenster)
            {
                _ausfaelle.Dequeue();
            }

            neueSperre = _ausfaelle.Count >= _options.UeberlastFehlerIn30s && jetzt >= _gesperrtBis;
            if (neueSperre)
            {
                _gesperrtBis = jetzt.AddSeconds(_options.UeberlastSperreSekunden);
                _ausfaelle.Clear();
            }
        }

        if (neueSperre)
        {
            EngineUeberlast(logger, _options.UeberlastFehlerIn30s, _options.UeberlastSperreSekunden);
        }
    }

    [LoggerMessage(EventName = "EngineUeberlast", Level = LogLevel.Warning,
        Message = "EngineUeberlast: mindestens {Fehler} Engine-Ausfaelle in 30 s, neue Gast-Partien fuer {Sekunden} s gesperrt")]
    private static partial void EngineUeberlast(ILogger logger, int fehler, int sekunden);
}
