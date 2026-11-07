using Schach.Application.Sprueche;

namespace Schach.Application.Tests.Sprueche;

/// <summary>SCHACH-SPEC 6.4: was aus der Modellantwort ein Spruch wird und was abgelehnt wird.</summary>
public class SpruchPrueferTests
{
    private static SpruchPruefung Pruefe(string roh, string name = "Finn") => SpruchPruefer.Pruefe(roh, name);

    [Fact]
    public void Ein_sauberer_Satz_bleibt_unveraendert()
    {
        var ergebnis = Pruefe("Attacke! Mein Springer ist schon unterwegs.");

        ergebnis.Ok.Should().BeTrue();
        ergebnis.Text.Should().Be("Attacke! Mein Springer ist schon unterwegs.");
    }

    [Fact]
    public void Trimmt_und_nimmt_nur_die_erste_Zeile()
    {
        var ergebnis = Pruefe("  \n Boah, das war knapp!  \nZweite Zeile, die keiner will.");

        ergebnis.Text.Should().Be("Boah, das war knapp!");
    }

    [Theory]
    [InlineData("\"Boah, das war knapp!\"")]
    [InlineData("„Boah, das war knapp!“")]
    [InlineData("»Boah, das war knapp!«")]
    [InlineData("'Boah, das war knapp!'")]
    public void Entfernt_Anfuehrungszeichen_aussen(string roh) =>
        Pruefe(roh).Text.Should().Be("Boah, das war knapp!");

    [Theory]
    [InlineData("Finn: Boah, das war knapp!")]
    [InlineData("finn : Boah, das war knapp!")]
    [InlineData("\"Finn: Boah, das war knapp!\"")]
    public void Entfernt_den_eigenen_Namen_am_Anfang(string roh) =>
        Pruefe(roh).Text.Should().Be("Boah, das war knapp!");

    [Fact]
    public void Laesst_eine_Aufzaehlung_mit_Doppelpunkt_stehen()
    {
        // Konrad nummeriert („Erstens: …“). Nur der eigene Name wird entfernt, nicht jedes Wort vor
        // einem Doppelpunkt.
        Pruefe("Erstens: Entwicklung, zweitens: Angriff.", "Konrad").Text
            .Should().Be("Erstens: Entwicklung, zweitens: Angriff.");
    }

    [Fact]
    public void Kuerzt_ueber_100_Zeichen_am_letzten_Satzzeichen()
    {
        var roh = "Mein Läufer stand lange still, jetzt endlich darf er los. "
                  + "Und dann kommt auch noch der Turm dazu, das wird ein großer Spaß hier!";
        roh.Length.Should().BeGreaterThan(100);

        var ergebnis = Pruefe(roh);

        ergebnis.Ok.Should().BeTrue();
        ergebnis.Text.Should().Be("Mein Läufer stand lange still, jetzt endlich darf er los.");
        ergebnis.RohLaengeUeber100.Should().BeTrue();
    }

    [Fact]
    public void Lehnt_ueber_100_Zeichen_ohne_Satzzeichen_ab()
    {
        var roh = string.Join(' ', Enumerable.Repeat("Attacke", 20));

        var ergebnis = Pruefe(roh);

        ergebnis.Ok.Should().BeFalse();
        ergebnis.Grund.Should().Be(SpruchAblehnung.ZuLang);
        ergebnis.RohLaengeUeber100.Should().BeTrue();
    }

    [Fact]
    public void Genau_100_Zeichen_sind_erlaubt()
    {
        var roh = new string('a', 99) + "!";

        Pruefe(roh).Ok.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("Finn:")]
    public void Lehnt_leere_Antworten_ab(string roh) =>
        Pruefe(roh).Grund.Should().Be(SpruchAblehnung.Leer);

    [Theory]
    [InlineData("**Attacke!** Mein Springer kommt.")]
    [InlineData("# Attacke")]
    [InlineData("- Attacke, mein Springer kommt.")]
    [InlineData("Mein `Springer` kommt.")]
    [InlineData("Mein __Springer__ kommt.")]
    public void Lehnt_Markdown_ab(string roh) =>
        Pruefe(roh).Grund.Should().Be(SpruchAblehnung.Markdown);

    [Theory]
    [InlineData("Schau mal auf https://example.invalid nach.")]
    [InlineData("Mehr auf www.example.invalid, ehrlich.")]
    public void Lehnt_URLs_ab(string roh) =>
        Pruefe(roh).Grund.Should().Be(SpruchAblehnung.Url);

    [Fact]
    public void Lehnt_offene_Platzhalter_ab() =>
        Pruefe("Mein {{figur}} ist weg.").Grund.Should().Be(SpruchAblehnung.Platzhalter);

    [Theory]
    [InlineData("Du solltest jetzt rochieren.")]
    [InlineData("Das könntest du besser machen.")]
    [InlineData("Du musst den König schützen.")]
    [InlineData("Da müsstest du nachdenken.")]
    [InlineData("Zieh doch den Turm.")]
    [InlineData("Ich ziehe gleich meine Dame.")]
    [InlineData("Nimm den Bauern.")]
    [InlineData("Pass auf deine Dame auf.")]
    [InlineData("PASS   AUF, jetzt kommt was.")]
    [InlineData("Achtung, mein Läufer kommt.")]
    [InlineData("Vorsicht mit dem König.")]
    [InlineData("Du solltest aufpassen.")]
    [InlineData("Dein Turm hängt.")]
    [InlineData("Dein Turm ist ungedeckt.")]
    [InlineData("Mein Läufer droht Matt.")]
    [InlineData("Besser wäre der Springer gewesen.")]
    [InlineData("Hättest du mal getauscht.")]
    [InlineData("Ich empfehle die Rochade.")]
    [InlineData("Kleiner Tipp: rochieren.")]
    [InlineData("Ich rate dir zur Rochade.")]
    [InlineData("Haben Sie das gesehen?")]
    [InlineData("Das gehört Ihnen.")]
    public void Lehnt_Sperrlisten_Woerter_ab(string roh) =>
        Pruefe(roh).Grund.Should().Be(SpruchAblehnung.Sperrliste);

    [Theory]
    [InlineData("Die Dame ist weg, sie war mir lieb.")]
    [InlineData("Wie schön, ihnen allen zuzusehen.")]
    [InlineData("Attacke! Der Zug war Absicht.")]
    [InlineData("Tippen ist nicht meins, ich spiele lieber.")]
    [InlineData("Mein Turm hängte sich nie auf.")]
    public void Wortgrenzen_und_kleines_sie_loesen_keinen_Treffer_aus(string roh) =>
        Pruefe(roh).Ok.Should().BeTrue();

    [Theory]
    [InlineData("Der Zug war dumm.")]
    [InlineData("So ein dummer Läufer!")]
    [InlineData("Doof gelaufen für dich.")]
    [InlineData("Was für ein doofer Zug.")]
    [InlineData("Blöd für deinen Turm.")]
    [InlineData("Der blöde Bauer ist weg.")]
    [InlineData("Wie ein Idiot gezogen.")]
    [InlineData("Na, du Trottel!")]
    [InlineData("Danke, Versager.")]
    [InlineData("Verloren, Loser!")]
    [InlineData("Danke, du Depp.")]
    public void Lehnt_Beleidigungen_ab(string roh) =>
        Pruefe(roh).Grund.Should().Be(SpruchAblehnung.Sperrliste);

    [Theory]
    [InlineData("Echt jetzt, der Läufer?")]
    [InlineData("Danke für die Dame, war lecker.")]
    [InlineData("Ich spiel doch nicht auf Remis, ich will gewinnen.")]
    public void Neckende_Saetze_ueber_Zuege_bleiben_erlaubt(string roh) =>
        Pruefe(roh).Ok.Should().BeTrue();
}
