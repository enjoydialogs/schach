namespace Schach.Infrastructure.Betrieb;

/// <summary>Abschnitt <c>Betrieb</c> (SCHACH-SPEC 14.2).</summary>
public sealed class BetriebOptions
{
    public const string Abschnitt = "Betrieb";

    /// <summary>Fenster fuer <c>EngineUeberlast</c> und <c>MailKontingent</c>: je Art hoechstens eine
    /// Warnzeile und eine Mail in diesem Zeitraum.</summary>
    public int WarnFensterMinuten { get; set; } = 60;

    /// <summary>Uhrzeit der Tageszeile in <see cref="Zeitzone"/>.</summary>
    public TimeOnly TageszeileUm { get; set; } = new(6, 0);

    /// <summary>IANA-Zeitzone fuer die Tageszeile. Der Container laeuft in UTC.</summary>
    public string Zeitzone { get; set; } = "Europe/Berlin";
}
