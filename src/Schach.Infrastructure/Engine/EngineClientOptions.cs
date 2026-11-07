namespace Schach.Infrastructure.Engine;

/// <summary>
/// Konfiguration des Engine-Clients (Abschnitt <c>Engine</c>, SCHACH-SPEC 5.2). Compose setzt
/// <c>Engine__BaseUrl</c>. Der Abschnitt traegt auch Schluessel anderer Sessions
/// (<c>Engine:Eichung</c>, S1b); diese Klasse liest nur ihre eigenen.
/// </summary>
public sealed class EngineClientOptions
{
    public const string Abschnitt = "Engine";

    /// <summary>Adresse des Engine-Dienstes, z. B. <c>http://engine:8081</c>.</summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>Aeusserer HttpClient-Timeout je Anfrage (5 s).</summary>
    public TimeSpan GesamtTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Frist je Maia-Versuch auf <c>/move</c> (3 s), danach ein Retry, dann Fairy-Stockfish.</summary>
    public TimeSpan MaiaTimeout { get; set; } = TimeSpan.FromSeconds(3);
}
