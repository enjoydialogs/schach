namespace Schach.Application.Limits;

/// <summary>
/// Abschnitt <c>Limits</c> (SCHACH-SPEC 11.2, 11.3). Alle Zahlen sind Vorgaben; die
/// Nebenlaeufigkeitszahlen sind Platzhalter bis zur VPS-Messung Teil 2. Mobilfunk-CGNAT kann
/// IP-Limits treffen: im Zweifel nach oben korrigieren (per Env, z. B. <c>Limits__GastPartienJeIpUndStunde</c>).
/// </summary>
public sealed class LimitsOptions
{
    public const string Abschnitt = "Limits";

    /// <summary>DB-Zaehlung je Spieler, rollierend 24 h; darueber 429 <c>TageslimitErreicht</c>.</summary>
    public int PartienJeSpielerUndTag { get; set; } = 50;

    /// <summary>Limiter <c>gast-partie-je-ip</c>, Sliding 1 h.</summary>
    public int GastPartienJeIpUndStunde { get; set; } = 60;

    /// <summary>DB-Zaehlung offener Gast-Partien; darueber 503 <c>GeradeVielLos</c>, nur Gaeste.</summary>
    public int OffeneGastPartienGesamt { get; set; } = 300;

    /// <summary>Limiter <c>zuege-je-partie</c>, Sliding 1 min je Partie.</summary>
    public int ZuegeJePartieUndMinute { get; set; } = 40;

    /// <summary>Limiter <c>tipp-je-partie</c>, Sliding 1 min je Partie.</summary>
    public int TippsJePartieUndMinute { get; set; } = 10;

    /// <summary>Limiter <c>aktion-je-partie</c>, Sliding 1 min je Partie.</summary>
    public int AktionenJePartieUndMinute { get; set; } = 20;

    /// <summary>GlobalLimiter: gleichzeitige <c>POST …/zuege</c>.</summary>
    public int EngineZuegeGleichzeitig { get; set; } = 24;

    public int EngineZuegeWarteschlange { get; set; } = 48;

    /// <summary>GlobalLimiter: gleichzeitige <c>POST …/tipp</c>.</summary>
    public int StockfishTippsGleichzeitig { get; set; } = 2;

    public int StockfishTippsWarteschlange { get; set; } = 4;

    /// <summary>Limiter <c>magic-link-je-ip</c>, FixedWindow 10 min (SCHACH-SPEC 11.3).</summary>
    public int AnmeldungenJeIpUnd10Minuten { get; set; } = 5;

    /// <summary>Limiter <c>sprachausgabe-je-ip</c>, Sliding 1 min (SCHACH-SPEC 6.9). Ein Spruch je
    /// Gegnerzug plus Wiederholen per Tipp; CGNAT teilt sich die IP.</summary>
    public int SprachausgabenJeIpUndMinute { get; set; } = 60;
}
