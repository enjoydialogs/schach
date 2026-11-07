using System.Net;
using System.Text;
using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Email;

/// <summary>Der eine Knopf einer Systemmail: Beschriftung und Ziel.</summary>
public sealed record SystemMailAction(string Label, string Url);

/// <summary>
/// Die Teile einer Systemmail, schon uebersetzt. Klartext und HTML entstehen aus denselben Teilen,
/// damit die beiden Fassungen nicht auseinanderlaufen. <see cref="CodeZeile"/> ist eine optionale
/// Zeile, die gross und monospace unter dem Knopf steht (Anmelde-Code, SCHACH-SPEC 9.6).
/// </summary>
public sealed record SystemMailContent(
    string Subject,
    string Preheader,
    string Heading,
    IReadOnlyList<string> Intro,
    SystemMailAction? Action,
    SystemMailCode? CodeZeile,
    IReadOnlyList<string> Outro,
    IReadOnlyList<string> Footer);

/// <summary>Eine Code-Zeile: Text davor und der Code selbst (gross, monospace).</summary>
public sealed record SystemMailCode(string Text, string Code);

/// <summary>
/// Mail-Layout nach Kronrat (<c>SystemMailLayout</c>, SCHACH-SPEC 13.3): eine Karte mit Wortmarke,
/// Ueberschrift und einem Knopf, dazu der Klartext-Teil. Reines HTML fuer Mailprogramme: Tabellen,
/// Stile inline, keine Bilder, keine Webfonts als Pflicht. Jede Zeichenkette laeuft durch
/// <see cref="WebUtility.HtmlEncode(string)"/>, auch das <c>href</c>.
/// </summary>
public static class SystemMailLayout
{
    private const string Accent = "#2f5d50";
    private const string AccentLine = "#4f8a78";
    private const string Paper = "#f3f2ee";
    private const string Card = "#ffffff";
    private const string CardBorder = "#dcdad2";
    private const string Ink = "#1c1d1f";
    private const string Body = "#2f3135";
    private const string Muted = "#6b6e74";
    private const string Rule = "#e8e6df";

    private const string Sans = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";
    private const string Mono = "'JetBrains Mono',Consolas,'Courier New',monospace";

    /// <summary>Baut die Mail: Klartext und HTML aus denselben Teilen.</summary>
    public static OutboundEmail Compose(string to, SystemMailContent content, string? replyTo = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new OutboundEmail(to, content.Subject, RenderText(content), RenderHtml(content), replyTo);
    }

    private static string RenderText(SystemMailContent c)
    {
        var text = new StringBuilder();
        text.Append(c.Heading).Append("\n\n");
        foreach (var p in c.Intro)
        {
            text.Append(p).Append("\n\n");
        }

        if (c.Action is not null)
        {
            text.Append(c.Action.Label).Append(":\n").Append(c.Action.Url).Append("\n\n");
        }

        if (c.CodeZeile is not null)
        {
            text.Append(c.CodeZeile.Text).Append(' ').Append(c.CodeZeile.Code).Append("\n\n");
        }

        foreach (var p in c.Outro)
        {
            text.Append(p).Append("\n\n");
        }

        if (c.Footer.Count > 0)
        {
            text.Append("-- \n").Append(string.Join("\n", c.Footer)).Append('\n');
        }

        return text.ToString().TrimEnd('\n') + "\n";
    }

    private static string RenderHtml(SystemMailContent c)
    {
        var inner = new StringBuilder()
            .Append("<h1 style=\"margin:0 0 16px;font-family:").Append(Sans)
            .Append(";font-size:22px;line-height:1.3;font-weight:700;color:").Append(Ink)
            .Append(";\" class=\"sm-text\">").Append(Encode(c.Heading)).Append("</h1>");
        foreach (var p in c.Intro)
        {
            inner.Append(Paragraph(p, Body, 16));
        }

        if (c.Action is not null)
        {
            inner.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:8px 0 24px;\">")
                .Append("<tr><td bgcolor=\"").Append(Accent).Append("\" style=\"border-radius:6px;background-color:")
                .Append(Accent).Append(";\">")
                .Append("<a href=\"").Append(Encode(c.Action.Url))
                .Append("\" target=\"_blank\" style=\"display:inline-block;padding:13px 28px;font-family:")
                .Append(Sans).Append(";font-size:16px;font-weight:600;line-height:20px;color:#ffffff;text-decoration:none;border-radius:6px;\">")
                .Append(Encode(c.Action.Label)).Append("</a></td></tr></table>");
        }

        if (c.CodeZeile is not null)
        {
            inner.Append(Paragraph(c.CodeZeile.Text, Body, 15))
                .Append("<p style=\"margin:0 0 24px;font-family:").Append(Mono)
                .Append(";font-size:30px;line-height:1.2;font-weight:700;letter-spacing:4px;color:").Append(Ink)
                .Append(";\" class=\"sm-text\">").Append(Encode(c.CodeZeile.Code)).Append("</p>");
        }

        foreach (var p in c.Outro)
        {
            inner.Append(Paragraph(p, Muted, 15));
        }

        return Shell(c.Subject, c.Preheader, inner.ToString(), c.Footer);
    }

    private static string Paragraph(string text, string color, int fontSize) =>
        $"<p style=\"margin:0 0 16px;font-family:{Sans};font-size:{fontSize}px;line-height:1.6;color:{color};\" class=\"{(color == Muted ? "sm-muted" : "sm-text")}\">{Encode(text)}</p>";

    private static string Shell(string title, string preheader, string inner, IReadOnlyList<string> footer)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"de\">\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<meta name=\"color-scheme\" content=\"light dark\">\n")
            .Append("<meta name=\"supported-color-schemes\" content=\"light dark\">\n")
            .Append("<title>").Append(Encode(title)).Append("</title>\n")
            // Nur Verfeinerung: wer <style> verwirft, sieht die Inline-Stile, und die tragen allein.
            .Append("<style>\n")
            .Append("@media (prefers-color-scheme: dark) {\n")
            .Append("  .sm-bg { background-color:#141414 !important; }\n")
            .Append("  .sm-card { background-color:#1f1f21 !important; border-color:#34343a !important; }\n")
            .Append("  .sm-text { color:#ececec !important; }\n")
            .Append("  .sm-muted { color:#a6a6ad !important; }\n")
            .Append("  .sm-word { color:#8fc7b5 !important; }\n")
            .Append("}\n")
            .Append("@media (max-width: 600px) { .sm-pad { padding-left:20px !important; padding-right:20px !important; } }\n")
            .Append("</style>\n</head>\n")
            .Append("<body style=\"margin:0;padding:0;background-color:").Append(Paper).Append(";\" class=\"sm-bg\">\n");

        if (!string.IsNullOrWhiteSpace(preheader))
        {
            // Die Vorschauzeile in der Posteingangsliste; im Text selbst unsichtbar.
            html.Append("<div style=\"display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;mso-hide:all;\">")
                .Append(Encode(preheader)).Append("</div>\n");
        }

        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" bgcolor=\"")
            .Append(Paper).Append("\" style=\"background-color:").Append(Paper).Append(";\" class=\"sm-bg\">\n")
            .Append("<tr><td align=\"center\" style=\"padding:32px 12px 40px;\">\n")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width:560px;\">\n")
            // Wortmarke als Text, kein Bild: kein Nachladen, keine Blockade, im Dunkelmodus lesbar.
            .Append("<tr><td class=\"sm-pad sm-word\" style=\"padding:0 32px 14px;font-family:").Append(Mono)
            .Append(";font-size:15px;font-weight:700;letter-spacing:5px;color:").Append(Accent).Append(";\">SCHACH</td></tr>\n")
            .Append("<tr><td bgcolor=\"").Append(Card).Append("\" class=\"sm-card\" style=\"background-color:").Append(Card)
            .Append(";border:1px solid ").Append(CardBorder).Append(";border-top:3px solid ").Append(AccentLine)
            .Append(";border-radius:4px;\">\n")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">")
            .Append("<tr><td class=\"sm-pad\" style=\"padding:32px 32px 28px;\">\n")
            .Append(inner)
            .Append("\n</td></tr></table>\n</td></tr>\n");

        if (footer.Count > 0)
        {
            html.Append("<tr><td class=\"sm-pad sm-muted\" style=\"padding:20px 32px 0;font-family:").Append(Sans)
                .Append(";font-size:12px;line-height:1.6;color:").Append(Muted).Append(";border-top:0 solid ").Append(Rule).Append(";\">")
                .Append(string.Join("<br>", footer.Select(Encode)))
                .Append("</td></tr>\n");
        }

        html.Append("</table>\n</td></tr>\n</table>\n</body>\n</html>\n");
        return html.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
