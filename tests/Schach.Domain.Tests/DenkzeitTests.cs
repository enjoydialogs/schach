namespace Schach.Domain.Tests;

/// <summary>Gegner-Denkzeit D nach SCHACH-SPEC 8.6 aus dem Partie-PRNG.</summary>
public sealed class DenkzeitTests
{
    [Fact]
    public void D_liegt_ohne_Uhr_zwischen_1_und_3_Sekunden()
    {
        for (var halbzug = 0; halbzug < 2_000; halbzug++)
        {
            Gegnerdenkzeit.Ms(seed: 4711, halbzug, gegnerRestMs: null).Should().BeInRange(1_000, 3_000);
        }
    }

    [Fact]
    public void D_nutzt_die_ganze_Spanne()
    {
        var werte = Enumerable.Range(0, 2_000).Select(h => Gegnerdenkzeit.Ms(99, h, null)).ToList();

        werte.Min().Should().BeLessThan(1_100);
        werte.Max().Should().BeGreaterThan(2_900);
    }

    [Fact]
    public void D_ist_je_Seed_und_Halbzug_deterministisch()
    {
        Gegnerdenkzeit.Ms(123, 7, null).Should().Be(Gegnerdenkzeit.Ms(123, 7, null));
        Enumerable.Range(0, 50).Select(h => Gegnerdenkzeit.Ms(123, h, null))
            .Should().NotEqual(Enumerable.Range(0, 50).Select(h => Gegnerdenkzeit.Ms(124, h, null)));
    }

    [Fact]
    public void Mit_Uhr_hoechstens_zehn_Prozent_der_Gegner_Restzeit()
    {
        for (var halbzug = 0; halbzug < 200; halbzug++)
        {
            Gegnerdenkzeit.Ms(5, halbzug, gegnerRestMs: 12_000).Should().BeLessThanOrEqualTo(1_200);
        }

        // Viel Restzeit: Deckel greift nicht.
        Gegnerdenkzeit.Ms(5, 3, gegnerRestMs: 900_000).Should().Be(Gegnerdenkzeit.Ms(5, 3, null));
    }
}
