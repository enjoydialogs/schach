using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Email;

/// <summary>Die Anmelde-Mail mit Link und Code, Texte nach SCHACH-SPEC 9.6.</summary>
public static class AnmeldeMail
{
    public const string Betreff = "Anmelden bei Schach";

    /// <param name="code">6 Ziffern; die Mail zeigt sie als „123 456“.</param>
    /// <param name="replyTo">Kontaktadresse fuer Reply-To und Fuss; <c>null</c> laesst beides weg.</param>
    public static OutboundEmail Erzeuge(string an, string link, string code, string? replyTo)
    {
        ArgumentNullException.ThrowIfNull(code);
        var gruppiert = code.Length == 6 ? $"{code[..3]} {code[3..]}" : code;

        List<string> fuss = ["Du bekommst diese Mail, weil jemand diese Adresse auf schach.enjoy-dialogs.de eingegeben hat."];
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            fuss.Add($"Fragen? Antworte einfach auf diese Mail oder schreib an {replyTo}.");
        }

        var inhalt = new SystemMailContent(
            Subject: Betreff,
            Preheader: "Link oder Code gelten 15 Minuten.",
            Heading: "Anmelden bei Schach",
            Intro: ["Tippe auf den Knopf, um dich anzumelden. Wenn du noch kein Spielerkonto hast, legen wir es dabei an."],
            Action: new SystemMailAction("Jetzt anmelden", link),
            CodeZeile: new SystemMailCode("Oder gib diesen Code in der App ein:", gruppiert),
            Outro: ["Link und Code gelten 15 Minuten und nur einmal. Wenn du das nicht warst, kannst du diese Mail ignorieren."],
            Footer: fuss);

        return SystemMailLayout.Compose(an, inhalt, string.IsNullOrWhiteSpace(replyTo) ? null : replyTo);
    }
}
