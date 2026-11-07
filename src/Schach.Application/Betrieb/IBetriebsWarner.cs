namespace Schach.Application.Betrieb;

/// <summary>Die vier Warnungen aus SCHACH-SPEC 14.2. Der Name ist zugleich das Wort am Anfang der
/// Logzeile und ihr EventName.</summary>
public enum BetriebsWarnung
{
    /// <summary>Mistral-Budget hat die Warnstufe erreicht (SCHACH-SPEC 11.1).</summary>
    BudgetWarn,

    /// <summary>Mistral-Budget ist erschoepft (SCHACH-SPEC 11.1).</summary>
    BudgetErschoepft,

    /// <summary>Die Engine hat die Ueberlastschwelle gerissen (SCHACH-SPEC 11.2).</summary>
    EngineUeberlast,

    /// <summary>Das globale Mail-Kontingent ist erschoepft (SCHACH-SPEC 11.3).</summary>
    MailKontingent,
}

/// <summary>
/// Meldet eine Betriebswarnung (SCHACH-SPEC 14.2). Je Art schreibt die erste Meldung eines Fensters
/// eine Warn-Logzeile und reiht eine Mail an <c>Email:OperatorEmail</c> ein; weitere Meldungen im
/// selben Fenster zaehlen nur fuer die Tageszeile.
///
/// Fenster: <see cref="BetriebsWarnung.BudgetWarn"/> und <see cref="BetriebsWarnung.BudgetErschoepft"/>
/// je Kalendermonat (UTC, wie <c>LlmVerbrauch.Monat</c>), die anderen je <c>Betrieb:WarnFensterMinuten</c>.
/// Die Sperre liegt im Speicher. Nach einem Neustart kann eine Budgetwarnung erneut ausgehen; der
/// Budgetwaechter (S2c) prueft dafuer <c>LlmVerbrauch.WarnGemeldetAm</c> / <c>ErschoepftGemeldetAm</c>
/// und ruft nur, wenn noch nicht gemeldet.
///
/// Synchron und wirft nie, damit Limiter-Callbacks und der Request-Pfad es direkt rufen koennen.
/// </summary>
public interface IBetriebsWarner
{
    /// <param name="art">Welche Warnung.</param>
    /// <param name="zusatz">Kurzer Text fuer Logzeile und Mail, z. B. „82 % von 5,00 €“.
    /// NIE eine IP, E-Mail-Adresse oder ein Mailinhalt (SCHACH-SPEC 14.3).</param>
    /// <returns><c>true</c>, wenn dies die erste Meldung dieser Art im aktuellen Fenster war.</returns>
    bool Melden(BetriebsWarnung art, string? zusatz = null);
}
