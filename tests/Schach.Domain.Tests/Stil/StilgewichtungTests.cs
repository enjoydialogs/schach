using Schach.Domain.Stil;

namespace Schach.Domain.Tests.Stil;

/// <summary>Stil-Algorithmus nach SCHACH-SPEC 5.3 mit eingespeisten Merkmalen und Remis-Flags.</summary>
public sealed class StilgewichtungTests
{
    private static readonly Stilparameter Neutral = new(Stilgewichte.Neutral, Temperatur: 1.0);

    private static readonly IReadOnlyList<Policyzug> Policy =
    [
        new("e2e4", 0.40),
        new("d2d4", 0.30),
        new("g1f3", 0.15),
        new("c2c4", 0.08),
        new("b1c3", 0.05),
        new("a2a3", 0.02),
    ];

    private static Zugmerkmal OhneMerkmale(string uci) => Zugmerkmal.Keine;

    [Fact]
    public void Kandidatenband_enthaelt_Zuege_ab_einem_Viertel_von_p_max()
    {
        // p_max = 0,40, Schwelle 0,10: e2e4, d2d4, g1f3.
        Stilgewichtung.Kandidatenband(Policy, Neutral).Should().Equal("e2e4", "d2d4", "g1f3");
    }

    [Fact]
    public void Kandidatenband_hat_hoechstens_acht_Zuege()
    {
        var breit = Enumerable.Range(0, 12).Select(i => new Policyzug($"z{i:00}", 1.0 / 12)).ToList();

        Stilgewichtung.Kandidatenband(breit, Neutral).Should().HaveCount(8);
    }

    [Fact]
    public void Gewicht_ist_Produkt_der_Merkmalsgewichte_begrenzt_auf_halb_bis_doppelt()
    {
        var aggressiv = Stilgewichte.Fuer(Persoenlichkeit.Aggressiv);
        var vorsichtig = Stilgewichte.Fuer(Persoenlichkeit.Vorsichtig);

        Stilgewichtung.Gewicht(Zugmerkmal.Keine, aggressiv).Should().Be(1.0);
        Stilgewichtung.Gewicht(Zugmerkmal.Schlagzug, aggressiv).Should().Be(1.4);
        Stilgewichtung.Gewicht(Zugmerkmal.Schlagzug | Zugmerkmal.Tausch, aggressiv).Should().BeApproximately(0.98, 1e-9);
        // 1,4 * 1,5 = 2,1 -> 2,0.
        Stilgewichtung.Gewicht(Zugmerkmal.Schlagzug | Zugmerkmal.Schach, aggressiv).Should().Be(2.0);
        // 0,7 * 0,5 = 0,35 -> 0,5.
        Stilgewichtung.Gewicht(Zugmerkmal.Koenigszone | Zugmerkmal.Riskant, vorsichtig).Should().Be(0.5);
    }

    [Fact]
    public void Gleiche_Partie_und_gleicher_Halbzug_ergeben_denselben_Zug()
    {
        for (var halbzug = 0; halbzug < 100; halbzug++)
        {
            var a = Stilgewichtung.Waehle(Policy, Neutral, OhneMerkmale, Remislage.Keine, seed: 42, halbzug);
            var b = Stilgewichtung.Waehle(Policy, Neutral, OhneMerkmale, Remislage.Keine, seed: 42, halbzug);
            a.Should().Be(b);
        }
    }

    [Fact]
    public void Reihenfolge_der_Policy_aendert_die_Wahl_nicht()
    {
        var gemischt = Policy.Reverse().ToList();

        for (var halbzug = 0; halbzug < 100; halbzug++)
        {
            Stilgewichtung.Waehle(gemischt, Neutral, OhneMerkmale, Remislage.Keine, 7, halbzug)
                .Should().Be(Stilgewichtung.Waehle(Policy, Neutral, OhneMerkmale, Remislage.Keine, 7, halbzug));
        }
    }

    [Fact]
    public void Neutral_mit_Temperatur_1_folgt_der_Policy()
    {
        var anteile = Haeufigkeiten(Policy, Neutral, OhneMerkmale, Remislage.Keine);

        foreach (var zug in Policy)
        {
            anteile.GetValueOrDefault(zug.Uci).Should().BeApproximately(zug.P, 0.015, zug.Uci);
        }
    }

    [Fact]
    public void Zug_ausserhalb_des_Bandes_bleibt_moeglich()
    {
        var anteile = Haeufigkeiten(Policy, Neutral, OhneMerkmale, Remislage.Keine);

        anteile.GetValueOrDefault("a2a3").Should().BeGreaterThan(0);
    }

    [Fact]
    public void Gewicht_wirkt_nur_im_Band()
    {
        var parameter = new Stilparameter(Stilgewichte.Fuer(Persoenlichkeit.Aggressiv), 1.0);
        // d2d4 (im Band) und a2a3 (ausserhalb) sind Schlagzuege mit Gewicht 1,4.
        Zugmerkmal Merkmale(string uci) => uci is "d2d4" or "a2a3" ? Zugmerkmal.Schlagzug : Zugmerkmal.Keine;

        var anteile = Haeufigkeiten(Policy, parameter, Merkmale, Remislage.Keine);

        // q: e2e4 0,40; d2d4 0,42; g1f3 0,15; c2c4 0,08; b1c3 0,05; a2a3 0,02; Summe 1,12.
        anteile["d2d4"].Should().BeApproximately(0.42 / 1.12, 0.015);
        anteile["e2e4"].Should().BeApproximately(0.40 / 1.12, 0.015);
        anteile.GetValueOrDefault("a2a3").Should().BeApproximately(0.02 / 1.12, 0.006);
    }

    [Fact]
    public void Niedrige_Temperatur_waehlt_den_hoechsten_Wert()
    {
        var kalt = Neutral with { Temperatur = 0.02 };

        var anteile = Haeufigkeiten(Policy, kalt, OhneMerkmale, Remislage.Keine);

        anteile["e2e4"].Should().BeGreaterThan(0.99);
    }

    [Fact]
    public void Hohe_Temperatur_streut_staerker()
    {
        var heiss = Neutral with { Temperatur = 2.0 };

        var anteile = Haeufigkeiten(Policy, heiss, OhneMerkmale, Remislage.Keine);

        anteile["e2e4"].Should().BeLessThan(0.33);
        anteile["a2a3"].Should().BeGreaterThan(0.04);
    }

    [Fact]
    public void Wahl_traegt_MaiaRang_PAnteil_und_W()
    {
        var parameter = new Stilparameter(Stilgewichte.Fuer(Persoenlichkeit.Aggressiv), 0.02);
        Zugmerkmal Merkmale(string uci) => uci == "d2d4" ? Zugmerkmal.Schlagzug | Zugmerkmal.Schach : Zugmerkmal.Keine;

        var wahl = Stilgewichtung.Waehle(Policy, parameter, Merkmale, Remislage.Keine, 1, 1);

        // q(d2d4) = 0,30 * 2,0 = 0,60 schlaegt q(e2e4) = 0,40.
        wahl.Uci.Should().Be("d2d4");
        wahl.MaiaRang.Should().Be(2);
        wahl.PAnteil.Should().BeApproximately(0.75, 1e-9);
        wahl.W.Should().Be(2.0);
    }

    [Fact]
    public void Ungewoehnlich_gilt_unter_halbem_p_max_und_wird_gewichtet()
    {
        var lehrerhaft = new Stilparameter(Stilgewichte.Fuer(Persoenlichkeit.Lehrerhaft), 0.02);
        // g1f3: p = 0,15 < 0,20 -> ungewoehnlich (0,5); Entwicklung (1,5): w = 0,75.
        Zugmerkmal Merkmale(string uci) => uci == "g1f3" ? Zugmerkmal.Entwicklung : Zugmerkmal.Keine;

        Stilgewichtung.Merkmale(Policy, "g1f3", Merkmale).Should().Be(Zugmerkmal.Entwicklung | Zugmerkmal.Ungewoehnlich);
        Stilgewichtung.Merkmale(Policy, "d2d4", Merkmale).Should().Be(Zugmerkmal.Keine);
        Stilgewichtung.Waehle(Policy, lehrerhaft, Merkmale, Remislage.Keine, 3, 9).Uci.Should().Be("e2e4");
    }

    [Fact]
    public void Klar_vorn_wird_nie_ein_Remiszug_gewaehlt()
    {
        var remis = new Remislage(Materialbilanz: 2, uci => uci is "e2e4" or "a2a3");

        var anteile = Haeufigkeiten(Policy, Neutral, OhneMerkmale, remis);

        anteile.Keys.Should().NotContain(["e2e4", "a2a3"]);
    }

    [Fact]
    public void Klar_vorn_bleibt_das_Band_nie_leer()
    {
        var remis = new Remislage(Materialbilanz: 5, uci => uci is "e2e4" or "d2d4" or "g1f3");

        var anteile = Haeufigkeiten(Policy, Neutral, OhneMerkmale, remis);

        // Alle Bandzuege loesen Remis aus: das Band bleibt, wie es ist.
        anteile.Keys.Should().Contain(["e2e4", "d2d4", "g1f3"]);
    }

    [Fact]
    public void Klar_hinten_wird_ein_Remiszug_aus_dem_Band_gewaehlt()
    {
        var remis = new Remislage(Materialbilanz: -2, uci => uci is "g1f3" or "a2a3");

        var anteile = Haeufigkeiten(Policy, Neutral, OhneMerkmale, remis);

        anteile.Keys.Should().Equal("g1f3");
    }

    [Fact]
    public void Klar_hinten_ohne_Remiszug_im_Band_bleibt_alles_wie_neutral()
    {
        var remis = new Remislage(Materialbilanz: -3, uci => uci == "a2a3");

        Haeufigkeiten(Policy, Neutral, OhneMerkmale, remis)
            .Should().BeEquivalentTo(Haeufigkeiten(Policy, Neutral, OhneMerkmale, Remislage.Keine));
    }

    [Fact]
    public void Knapp_vorn_spielt_Remis_keine_Rolle()
    {
        var remis = new Remislage(Materialbilanz: 1, uci => uci == "e2e4");

        Haeufigkeiten(Policy, Neutral, OhneMerkmale, remis)
            .Should().BeEquivalentTo(Haeufigkeiten(Policy, Neutral, OhneMerkmale, Remislage.Keine));
    }

    private static Dictionary<string, double> Haeufigkeiten(
        IReadOnlyList<Policyzug> policy, Stilparameter parameter, Func<string, Zugmerkmal> merkmale, Remislage remis)
    {
        const int Laeufe = 20_000;
        return Enumerable.Range(0, Laeufe)
            .Select(seed => Stilgewichtung.Waehle(policy, parameter, merkmale, remis, seed, 12).Uci)
            .GroupBy(u => u)
            .ToDictionary(g => g.Key, g => g.Count() / (double)Laeufe);
    }
}
