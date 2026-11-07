using Schach.Infrastructure.Email;

namespace Schach.Infrastructure.Tests.Email;

/// <summary>Texte der Anmelde-Mail nach SCHACH-SPEC 9.6.</summary>
public sealed class AnmeldeMailTests
{
    private const string Link = "https://schach.enjoy-dialogs.de/anmelden#t=abc_DEF-123";

    private static readonly Schach.Application.Abstractions.OutboundEmail Mail =
        AnmeldeMail.Erzeuge("anna@example.de", Link, "042917", "kontakt@enjoy-dialogs.de");

    [Fact]
    public void Betreff_ohne_Code_und_Empfaenger_und_Reply_To()
    {
        Mail.Subject.Should().Be("Anmelden bei Schach");
        Mail.To.Should().Be("anna@example.de");
        Mail.ReplyTo.Should().Be("kontakt@enjoy-dialogs.de");
    }

    [Fact]
    public void Textteil_traegt_alle_Bausteine_aus_9_6()
    {
        Mail.TextBody.Should()
            .Contain("Anmelden bei Schach")
            .And.Contain("Tippe auf den Knopf, um dich anzumelden. Wenn du noch kein Spielerkonto hast, legen wir es dabei an.")
            .And.Contain("Jetzt anmelden")
            .And.Contain(Link)
            .And.Contain("Oder gib diesen Code in der App ein: 042 917")
            .And.Contain("Link und Code gelten 15 Minuten und nur einmal. Wenn du das nicht warst, kannst du diese Mail ignorieren.")
            .And.Contain("Du bekommst diese Mail, weil jemand diese Adresse auf schach.enjoy-dialogs.de eingegeben hat.")
            .And.Contain("kontakt@enjoy-dialogs.de");
    }

    [Fact]
    public void Html_hat_Vorschau_Knopf_und_grossen_Monospace_Code()
    {
        Mail.HtmlBody.Should()
            .Contain("Link oder Code gelten 15 Minuten.")
            .And.Contain($"href=\"{Link}\"")
            .And.Contain(">Jetzt anmelden</a>")
            .And.Contain("042 917")
            .And.Contain("monospace");
    }

    [Fact]
    public void Html_hat_keine_Bilder_und_keine_Fremd_Ressourcen()
    {
        Mail.HtmlBody.Should().NotContain("<img").And.NotContain("<script").And.NotContain("http://");
    }
}
