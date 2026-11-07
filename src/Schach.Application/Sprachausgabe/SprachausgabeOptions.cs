namespace Schach.Application.Sprachausgabe;

/// <summary>
/// Abschnitt <c>Sprachausgabe</c> (SCHACH-SPEC 6.9, 11.1). Stimmen kommen nur aus der Konfiguration
/// (Dokploy-Env), nie aus dem Repo.
/// </summary>
public sealed class SprachausgabeOptions
{
    public const string SectionName = "Sprachausgabe";

    /// <summary>Hauptschalter. Aus heisst: <c>stimme</c> ist immer <c>null</c>.</summary>
    public bool Aktiv { get; set; } = true;

    /// <summary>API-Basis (<c>…/v1</c>), EU-Endpunkt wie bei den Spruechen.</summary>
    public string Endpoint { get; set; } = "https://api.eu.mistral.ai/v1";

    /// <summary>Datierter Bezeichner. Teil des Cache-Schluessels.</summary>
    public string Model { get; set; } = "voxtral-mini-tts-3-0";

    /// <summary>Zeitlimit eines Mistral-Aufrufs; danach Stille (204).</summary>
    public int ZeitlimitMs { get; set; } = 8000;

    /// <summary>Eigener weicher Deckel je Monat; <c>0</c> = Sprachausgabe aus.</summary>
    public int MonthlyBudgetEuroCents { get; set; } = 500;

    public int WarnThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Preis in Euro-Cent je Million Zeichen. Mistral nennt 13 $; die Vorgabe rechnet bewusst nicht
    /// um und liegt damit auf der sicheren Seite.
    /// </summary>
    public int CentJeMillionZeichen { get; set; } = 1300;

    public int BudgetCacheSekunden { get; set; } = 60;

    public int PauseNach429Sekunden { get; set; } = 60;

    /// <summary>Sweep-Block E: Cache-Eintraege, die so lange nicht genutzt wurden, verschwinden.</summary>
    public int CacheTage { get; set; } = 90;

    /// <summary>Je Gegner-Key, z. B. <c>Sprachausgabe__Stimmen__oscar__VoiceId</c>. Fehlt die Id, bleibt der Gegner stumm.</summary>
    public Dictionary<string, StimmeOptions> Stimmen { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Stimme eines Gegners bei Mistral.</summary>
public sealed class StimmeOptions
{
    public string VoiceId { get; set; } = "";

    /// <summary>1,0 bis 5,0; leer = Feld weglassen.</summary>
    public double? Expressiveness { get; set; }
}
