namespace Schach.Infrastructure.Email;

/// <summary>Abschnitt <c>Email</c> (SCHACH-SPEC 11.5).</summary>
public sealed class EmailOptionen
{
    public const string Abschnitt = "Email";

    /// <summary><c>Lettermint</c>, <c>Console</c> oder <c>File</c>. Leer: Lettermint, wenn ein Token
    /// gesetzt ist, sonst Console (SCHACH-SPEC 13.4).</summary>
    public string? Provider { get; set; }

    /// <summary>Absender, z. B. <c>Schach &lt;anmeldung@schach.enjoy-dialogs.de&gt;</c>.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Reply-To und Kontakt im Fuss der Anmelde-Mail.</summary>
    public string? ReplyToAddress { get; set; }

    /// <summary>Empfaenger der Budget- und Warnmails (S2c, S2d).</summary>
    public string? OperatorEmail { get; set; }

    /// <summary>Zielordner des <see cref="FileEmailSender"/> (Api.Tests).</summary>
    public string? FileDirectory { get; set; }

    public LettermintOptionen Lettermint { get; set; } = new();

    /// <summary>Der wirksame Provider nach der Regel oben.</summary>
    public string WirksamerProvider =>
        !string.IsNullOrWhiteSpace(Provider) ? Provider
        : !string.IsNullOrWhiteSpace(Lettermint.ApiToken) ? "Lettermint"
        : "Console";
}

/// <summary>Abschnitt <c>Email:Lettermint</c>.</summary>
public sealed class LettermintOptionen
{
    /// <summary>Projekt-Token (Geheimnis, nur im Dokploy-Panel: <c>Email__Lettermint__ApiToken</c>).</summary>
    public string? ApiToken { get; set; }

    public string BaseUrl { get; set; } = "https://api.lettermint.co/v1/";
}

/// <summary>Abschnitt <c>Mail</c>: globales Kontingent (SCHACH-SPEC 11.3).</summary>
public sealed class MailLimits
{
    public const string Abschnitt = "Mail";

    public int MaxJeStunde { get; set; } = 40;

    /// <summary>An den gebuchten Lettermint-Tarif anpassen.</summary>
    public int MaxJeTag { get; set; } = 200;
}
