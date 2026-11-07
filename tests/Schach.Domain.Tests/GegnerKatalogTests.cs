using Schach.Domain.Entitaeten;

namespace Schach.Domain.Tests;

/// <summary>Gegner-Katalog nach SCHACH-SPEC 4.1 bis 4.3 und Platzhalter-Eichung nach 5.4.</summary>
public sealed class GegnerKatalogTests
{
    [Fact]
    public void Acht_Gegner_in_aufsteigender_Elo()
    {
        GegnerKatalog.Alle.Select(g => (g.Key, g.Name, g.Elo, g.Stufe, g.Persoenlichkeit)).Should().Equal(
            ("ilse", "Ilse", 600, Stufe.Anfaenger, Persoenlichkeit.Gemuetlich),
            ("finn", "Finn", 800, Stufe.Anfaenger, Persoenlichkeit.Aggressiv),
            ("oscar", "Oscar", 1000, Stufe.Hobbyspieler, Persoenlichkeit.Lehrerhaft),
            ("nele", "Nele", 1200, Stufe.Hobbyspieler, Persoenlichkeit.Verspielt),
            ("theo", "Theo", 1400, Stufe.Vereinsspieler, Persoenlichkeit.Vorsichtig),
            ("sementa", "Sementa", 1600, Stufe.Vereinsspieler, Persoenlichkeit.Eitel),
            ("konrad", "Konrad", 1900, Stufe.Turnierspieler, Persoenlichkeit.Lehrerhaft),
            ("maren", "Maren", 2200, Stufe.Meister, Persoenlichkeit.Aggressiv));
    }

    [Fact]
    public void Vorstellungssaetze_stehen_wortgetreu_im_Katalog()
    {
        GegnerKatalog.Finde("ilse")!.Vorstellung.Should().Be("Keine Eile. Wir spielen in aller Ruhe, ja?");
        GegnerKatalog.Finde("maren")!.Vorstellung.Should().Be("Ich opfere gern eine Figur für den Angriff.");
        GegnerKatalog.Alle.Should().OnlyContain(g => g.Vorstellung.Length > 0);
    }

    [Fact]
    public void Unbekannter_Key_liefert_null()
    {
        GegnerKatalog.Finde("magnus").Should().BeNull();
    }

    [Fact]
    public void Startgewichte_aus_Tabelle_4_2()
    {
        // Reihenfolge: Schlagzug, Schach, Koenigszone, Riskant, Tausch, Rueckzug, Entwicklung, Ungewoehnlich.
        GewichteVon("finn").Should().Equal(1.4, 1.5, 1.5, 1.3, 0.7, 0.6, 1.0, 1.0);
        GewichteVon("theo").Should().Equal(1.0, 0.9, 0.7, 0.5, 1.0, 1.2, 1.2, 0.7);
        GewichteVon("oscar").Should().Equal(1.0, 1.0, 1.0, 0.8, 1.0, 1.0, 1.5, 0.5);
        GewichteVon("sementa").Should().Equal(1.1, 1.2, 1.2, 1.1, 0.5, 1.0, 1.0, 1.2);
        GewichteVon("ilse").Should().Equal(0.9, 0.9, 0.8, 0.6, 1.5, 1.0, 1.0, 0.8);
    }

    [Fact]
    public void Verspielt_hat_Ungewoehnlich_1_0_bis_die_Eichung_b_besteht()
    {
        // Spec 4.2: 1.5 nur nach bestandener Unabhaengigkeitspruefung (5.4 b); bis dahin 1.0.
        GewichteVon("nele").Should().Equal(1.1, 1.2, 1.0, 1.4, 0.8, 1.0, 0.9, 1.0);
        Stilgewichte.Fuer(Persoenlichkeit.Verspielt, unabhaengigkeitGeprueft: true).Ungewoehnlich.Should().Be(1.5);
    }

    [Fact]
    public void Platzhalter_Eichung_ist_Identitaet()
    {
        foreach (var gegner in GegnerKatalog.Alle)
        {
            var eichung = Eichung.Identitaet(gegner);
            eichung.SelfElo.Should().Be(gegner.Elo);
            eichung.Temperatur.Should().Be(1.0);
            eichung.Engine.Should().Be(ZugEngine.Maia);
        }
    }

    private static double[] GewichteVon(string key)
    {
        var g = GegnerKatalog.Finde(key)!.Gewichte;
        return [g.Schlagzug, g.Schach, g.Koenigszone, g.Riskant, g.Tausch, g.Rueckzug, g.Entwicklung, g.Ungewoehnlich];
    }
}
