using Schach.Application.Sprueche;
using Schach.Domain.Entitaeten;

namespace Schach.Application.Tests.Sprueche;

/// <summary>SCHACH-SPEC 6.5: Format, Pruefung und Auswahl der Ersatzsprueche.</summary>
public class ErsatzKatalogTests
{
    private const string Gueltig = """
        # Finn

        ## Partiebeginn
        - Los geht's!
        - Attacke von Anfang an!
        - Ich bin bereit.

        ## Sieg
        - Gewonnen!
        - Boah, Sieg!
        - Revanche gern.

        ## Niederlage
        - Mist, verloren.
        - Nächstes Mal!
        - Gut gespielt.

        ## Remis
        - Remis, okay.
        - Unentschieden, hm.
        - Na gut, Remis.

        ## RemisAbgelehnt
        - Nein, weiter!
        - Ich will noch angreifen.
        - Kein Remis heute.

        ## Zuruecknahme
        - Okay, nochmal.
        - Klar, zurück damit.
        - Na gut.

        ## FigurGewonnen
        - Ich hab {{figur_akk}}!
        - Jawoll, {{figur}} gehört mir.
        - Zack, geschnappt!

        ## FigurVerloren
        - Mist, {{figur}} ist weg.
        - Autsch, Zug {{zugnr}}.
        - Das tat weh.

        ## SchachGegeben
        - Schach!
        - Attacke, Schach!

        ## SchachErhalten
        - Uff, Schach.
        - Oh, mein König.

        ## Zug
        - Weiter geht's.
        - Mein Plan läuft.
        - Hm, spannend.
        - Ich hab da was vor.
        """;

    private static ErsatzKatalog Katalog(string inhalt = Gueltig) =>
        new(new Dictionary<string, IReadOnlyDictionary<SpruchEreignis, IReadOnlyList<string>>>
        {
            ["finn"] = ErsatzDatei.Parse(inhalt, "finn.de.md"),
        });

    [Fact]
    public void Parse_liest_Abschnitte_je_Ereignis()
    {
        var datei = ErsatzDatei.Parse(Gueltig, "finn.de.md");

        datei[SpruchEreignis.Partiebeginn].Should().Equal("Los geht's!", "Attacke von Anfang an!", "Ich bin bereit.");
        datei[SpruchEreignis.Zug].Should().HaveCount(4);
        datei.Should().NotContainKey(SpruchEreignis.Tausch);
    }

    [Fact]
    public void Parse_wirft_bei_unbekanntem_Ereignis()
    {
        var act = () => ErsatzDatei.Parse("## Rochade\n- Zack.", "x.de.md");

        act.Should().Throw<FormatException>().WithMessage("*Rochade*");
    }

    [Fact]
    public void Parse_wirft_bei_Zeile_ausserhalb_eines_Abschnitts()
    {
        var act = () => ErsatzDatei.Parse("- Zack.", "x.de.md");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Pruefe_meldet_nichts_fuer_eine_vollstaendige_Datei() =>
        ErsatzDatei.Pruefe(ErsatzDatei.Parse(Gueltig, "finn.de.md"), "Finn").Should().BeEmpty();

    [Fact]
    public void Pruefe_meldet_zu_wenige_Varianten()
    {
        var ohneEinenSieg = Gueltig.Replace("- Boah, Sieg!", "", StringComparison.Ordinal);

        ErsatzDatei.Pruefe(ErsatzDatei.Parse(ohneEinenSieg, "finn.de.md"), "Finn")
            .Should().ContainSingle().Which.Should().Contain("Sieg");
    }

    [Fact]
    public void Pruefe_meldet_unbekannte_Platzhalter_und_Sperrliste()
    {
        var kaputt = Gueltig
            .Replace("- Weiter geht's.", "- Mein {{zug}} war gut.", StringComparison.Ordinal)
            .Replace("- Hm, spannend.", "- Pass auf, gleich knallt's.", StringComparison.Ordinal);

        var fehler = ErsatzDatei.Pruefe(ErsatzDatei.Parse(kaputt, "finn.de.md"), "Finn");

        fehler.Should().HaveCount(2);
    }

    [Fact]
    public void Pruefe_meldet_Figur_am_Satzanfang()
    {
        // „der Turm ist weg“ stuende klein am Satzanfang.
        var kaputt = Gueltig.Replace("- Das tat weh.", "- {{figur}} ist weg.", StringComparison.Ordinal);

        ErsatzDatei.Pruefe(ErsatzDatei.Parse(kaputt, "finn.de.md"), "Finn")
            .Should().ContainSingle().Which.Should().Contain("Satzanfang");
    }

    [Fact]
    public void Waehle_setzt_Platzhalter_ein()
    {
        var katalog = Katalog();
        var werte = new ErsatzWerte('r', 17);

        var texte = Enumerable.Range(0, 30)
            .Select(h => katalog.Waehle("finn", SpruchEreignis.FigurGewonnen, 42, h, [], werte))
            .Distinct()
            .ToList();

        texte.Should().BeEquivalentTo("Ich hab den Turm!", "Jawoll, der Turm gehört mir.", "Zack, geschnappt!");
    }

    [Fact]
    public void Waehle_setzt_Zugnummer_und_Rueckfall_fuer_unbekannte_Figur_ein()
    {
        var katalog = Katalog();
        var werte = new ErsatzWerte(null, 23);

        var texte = Enumerable.Range(0, 30)
            .Select(h => katalog.Waehle("finn", SpruchEreignis.FigurVerloren, 7, h, [], werte))
            .ToHashSet();

        texte.Should().Contain("Mist, eine Figur ist weg.").And.Contain("Autsch, Zug 23.");
    }

    [Fact]
    public void Waehle_ist_deterministisch_je_Seed_und_Halbzug()
    {
        var katalog = Katalog();
        var werte = new ErsatzWerte(null, 1);

        var a = katalog.Waehle("finn", SpruchEreignis.Zug, 1234, 9, [], werte);
        var b = katalog.Waehle("finn", SpruchEreignis.Zug, 1234, 9, [], werte);

        a.Should().Be(b);
    }

    [Fact]
    public void Waehle_streut_ueber_Halbzuege()
    {
        var katalog = Katalog();

        var texte = Enumerable.Range(0, 40)
            .Select(h => katalog.Waehle("finn", SpruchEreignis.Zug, 99, h, [], new ErsatzWerte(null, 1)))
            .ToHashSet();

        texte.Should().HaveCount(4);
    }

    [Fact]
    public void Waehle_wiederholt_nichts_was_in_der_Partie_schon_gesagt_wurde()
    {
        var katalog = Katalog();
        var gesagt = new List<string>();

        for (var halbzug = 0; halbzug < 4; halbzug++)
        {
            gesagt.Add(katalog.Waehle("finn", SpruchEreignis.Zug, 5, halbzug, gesagt, new ErsatzWerte(null, 1)));
        }

        gesagt.Should().OnlyHaveUniqueItems().And.HaveCount(4);
    }

    [Fact]
    public void Waehle_wiederholt_erst_wenn_alle_Varianten_verbraucht_sind()
    {
        var katalog = Katalog();
        string[] alle = ["Weiter geht's.", "Mein Plan läuft.", "Hm, spannend.", "Ich hab da was vor."];

        katalog.Waehle("finn", SpruchEreignis.Zug, 5, 10, alle, new ErsatzWerte(null, 1))
            .Should().BeOneOf(alle);
    }

    [Theory]
    [InlineData(SpruchEreignis.Tausch)]
    [InlineData(SpruchEreignis.StellungBesser)]
    [InlineData(SpruchEreignis.StellungSchlechter)]
    public void Fehlende_optionale_Ereignisse_fallen_auf_Zug_zurueck(SpruchEreignis ereignis) =>
        Katalog().Waehle("finn", ereignis, 1, 1, [], new ErsatzWerte(null, 1))
            .Should().BeOneOf("Weiter geht's.", "Mein Plan läuft.", "Hm, spannend.", "Ich hab da was vor.");

    [Fact]
    public void Unbekannter_Gegner_wirft()
    {
        var act = () => Katalog().Waehle("niemand", SpruchEreignis.Zug, 1, 1, [], new ErsatzWerte(null, 1));

        act.Should().Throw<KeyNotFoundException>();
    }
}
