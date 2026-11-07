namespace Schach.Domain.Tests;

/// <summary>Frechheit und Laune je Spruch nach SCHACH-SPEC 4.2 und 6.3.</summary>
public sealed class LaunenwahlTests
{
    [Fact]
    public void Frechheit_je_Gegner_aus_Tabelle_4_3()
    {
        GegnerKatalog.Alle.Select(g => (g.Key, g.Frechheit)).Should().Equal(
            ("ilse", Frechheit.Freundlich),
            ("finn", Frechheit.Vorlaut),
            ("oscar", Frechheit.Freundlich),
            ("nele", Frechheit.Neckend),
            ("theo", Frechheit.Freundlich),
            ("sementa", Frechheit.Vorlaut),
            ("konrad", Frechheit.Neckend),
            ("maren", Frechheit.Vorlaut));
    }

    [Fact]
    public void Gleicher_Seed_und_Halbzug_ergibt_dieselbe_Laune()
    {
        foreach (var gegner in GegnerKatalog.Alle)
        {
            var erste = Enumerable.Range(1, 80).Select(h => Launenwahl.Waehle(gegner, 2026, h)).ToList();
            var zweite = Enumerable.Range(1, 80).Select(h => Launenwahl.Waehle(gegner, 2026, h)).ToList();
            erste.Should().Equal(zweite, gegner.Key);
        }
    }

    [Fact]
    public void Laune_wechselt_ueber_die_Partie()
    {
        foreach (var gegner in GegnerKatalog.Alle)
        {
            Enumerable.Range(1, 80).Select(h => Launenwahl.Waehle(gegner, 7, h)).Distinct().Count()
                .Should().BeGreaterThan(1, gegner.Key);
        }
    }

    [Theory]
    [InlineData(Frechheit.Freundlich, new[] { Laune.Trocken, Laune.Lustig })]
    [InlineData(Frechheit.Neckend, new[] { Laune.Trocken, Laune.Lustig, Laune.Vorlaut })]
    [InlineData(Frechheit.Vorlaut, new[] { Laune.Trocken, Laune.Lustig, Laune.Vorlaut, Laune.Genervt })]
    public void Vorlaut_und_genervt_nie_unter_ihrer_Stufe_auch_bei_falschen_Gewichten(Frechheit frechheit, Laune[] erlaubt)
    {
        // Gewichte, die vorlaut und genervt stark bevorzugen: die Stufe muss trotzdem greifen.
        var gewichte = new Launengewichte(1, 1, 10, 10);
        var launen = Enumerable.Range(0, 50)
            .SelectMany(seed => Enumerable.Range(1, 40).Select(h => Launenwahl.Waehle(frechheit, gewichte, seed, h)))
            .Distinct()
            .ToList();

        launen.Should().BeEquivalentTo(erlaubt);
    }

    [Fact]
    public void Ohne_erlaubtes_Gewicht_bleibt_der_Gegner_trocken()
    {
        Launenwahl.Waehle(Frechheit.Freundlich, new Launengewichte(0, 0, 3, 3), 1, 1).Should().Be(Laune.Trocken);
    }

    [Fact]
    public void Katalog_Gegner_sagen_nie_etwas_ueber_ihrer_Stufe()
    {
        foreach (var gegner in GegnerKatalog.Alle)
        {
            var launen = Enumerable.Range(0, 30)
                .SelectMany(seed => Enumerable.Range(1, 40).Select(h => Launenwahl.Waehle(gegner, seed, h)))
                .ToHashSet();

            if (gegner.Frechheit < Frechheit.Neckend)
            {
                launen.Should().NotContain(Laune.Vorlaut, gegner.Key);
            }

            if (gegner.Frechheit < Frechheit.Vorlaut)
            {
                launen.Should().NotContain(Laune.Genervt, gegner.Key);
            }
            else
            {
                launen.Should().Contain([Laune.Vorlaut, Laune.Genervt], gegner.Key);
            }
        }
    }

    [Fact]
    public void Finn_und_Sementa_reden_oefter_alle_anderen_wie_konfiguriert()
    {
        GegnerKatalog.Alle.Select(g => (g.Key, g.AbstandTierB, g.AbstandTierC)).Should().Equal(
            ("ilse", (int?)null, (int?)null),
            ("finn", 1, 2),
            ("oscar", null, null),
            ("nele", null, null),
            ("theo", null, null),
            ("sementa", 1, 2),
            ("konrad", null, null),
            ("maren", null, null));
    }
}
