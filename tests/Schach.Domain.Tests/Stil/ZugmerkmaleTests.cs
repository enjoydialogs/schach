using Schach.Domain.Stil;

namespace Schach.Domain.Tests.Stil;

/// <summary>Zugmerkmale nach SCHACH-SPEC 5.3 Schritt 4 als reine Funktionen (FEN vor dem Zug, UCI).</summary>
public sealed class ZugmerkmaleTests
{
    private const string Grundstellung = SchachDomain.GrundstellungFen;

    // Weiss: Ke1, Dd1, Lc4, Sf3, Bauern e4 d2; Schwarz: Ke8, Sc6, Lf8, Bauern e5 d7 f7, Dh4.
    private const string Italienisch = "r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4";

    [Theory]
    [InlineData("4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1", "e4d5", true)]
    [InlineData("4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1", "e4e5", false)]
    // En passant: e5xd6.
    [InlineData("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1", "e5d6", true)]
    public void Schlagzug(string fen, string uci, bool erwartet)
    {
        Zugmerkmale.Schlagzug(fen, uci).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(Italienisch, "c4f7", true)]
    [InlineData(Italienisch, "f3g5", false)]
    // Abzugsschach: Laeufer zieht weg, Turm e1 gibt Schach.
    [InlineData("4k3/8/8/8/4B3/8/8/K3R3 w - - 0 1", "e4h7", true)]
    // Umwandlung in eine Dame mit Schach.
    [InlineData("k7/4P3/8/8/8/8/8/4K3 w - - 0 1", "e7e8q", true)]
    [InlineData("k7/4P3/8/8/8/8/8/4K3 w - - 0 1", "e7e8n", false)]
    public void Schach(string fen, string uci, bool erwartet)
    {
        Zugmerkmale.Schach(fen, uci).Should().Be(erwartet);
    }

    [Theory]
    // Schwarzer Koenig e8: f6 ist 2 Reihen entfernt, Koenigszone.
    [InlineData(Italienisch, "f3g5", false)]
    [InlineData(Italienisch, "c4f7", true)]
    [InlineData("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", "a1a6", false)]
    [InlineData("4k3/8/8/8/8/8/8/2R1K3 w - - 0 1", "c1c6", true)]
    public void Koenigszone_Zielfeld_hoechstens_zwei_Felder_vom_gegnerischen_Koenig(string fen, string uci, bool erwartet)
    {
        Zugmerkmale.Koenigszone(fen, uci).Should().Be(erwartet);
    }

    [Fact]
    public void Unerlaubter_Zug_wird_abgelehnt()
    {
        var turmDiagonal = () => Zugmerkmale.Bestimme("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", "a1c3");

        turmDiagonal.Should().Throw<ArgumentException>();
    }

    [Theory]
    // Springer nach d5: vom Bauern e6 angegriffen (niedrigere Figur), auch wenn gedeckt.
    [InlineData("4k3/8/4p3/8/8/2N5/3P4/4K3 w - - 0 1", "c3d5", true)]
    // Springer nach b5: von nichts angegriffen.
    [InlineData("4k3/8/4p3/8/8/2N5/3P4/4K3 w - - 0 1", "c3b5", false)]
    // Springer nach e4: vom Turm e8 angegriffen und ungedeckt.
    [InlineData("4r1k1/8/8/8/8/2N5/8/K7 w - - 0 1", "c3e4", true)]
    // Springer nach e4: vom Turm angegriffen, aber vom Bauern d3 gedeckt (Turm ist hoeher).
    [InlineData("4r1k1/8/8/8/8/2NP4/8/K7 w - - 0 1", "c3e4", false)]
    // Dame nach h5: vom Springer f6 angegriffen (niedrigere Figur).
    [InlineData("4k3/8/5n2/8/8/8/8/3QK3 w - - 0 1", "d1h5", true)]
    // Koenigszuege sind nie riskant.
    [InlineData("4k3/8/8/8/8/8/8/4K3 w - - 0 1", "e1d1", false)]
    public void Riskant_angegriffen_und_ungedeckt_oder_von_niedrigerer_Figur(string fen, string uci, bool erwartet)
    {
        Zugmerkmale.Riskant(fen, uci).Should().Be(erwartet);
    }

    [Theory]
    // Springer schlaegt Laeufer, der Bauer kann zurueckschlagen.
    [InlineData("4k3/8/2p5/3b4/8/4N3/8/4K3 w - - 0 1", "e3d5", true)]
    // Springer schlaegt Laeufer ohne moeglichen Rueckschlag.
    [InlineData("4k3/8/8/3b4/8/4N3/8/4K3 w - - 0 1", "e3d5", false)]
    // Springer schlaegt Turm (nicht gleichwertig), Rueckschlag moeglich.
    [InlineData("4k3/8/2p5/3r4/8/4N3/8/4K3 w - - 0 1", "e3d5", false)]
    // Dame gegen Dame: ohne und mit Rueckschlag durch den Turm.
    [InlineData("7k/8/8/3q4/8/8/8/K2Q4 w - - 0 1", "d1d5", false)]
    [InlineData("3r3k/8/8/3q4/8/8/8/K2Q4 w - - 0 1", "d1d5", true)]
    public void Tausch_gleichwertiger_Schlag_mit_moeglichem_Rueckschlag(string fen, string uci, bool erwartet)
    {
        Zugmerkmale.Tausch(fen, uci).Should().Be(erwartet);
    }

    [Theory]
    [InlineData("4k3/8/8/8/4N3/8/8/4K3 w - - 0 1", "e4c3", true)]
    [InlineData("4k3/8/8/8/4N3/8/8/4K3 w - - 0 1", "e4c5", false)]
    // Seitwaerts ist kein Rueckzug.
    [InlineData("4k3/8/8/8/4R3/8/8/4K3 w - - 0 1", "e4a4", false)]
    // Schwarz zieht Richtung Reihe 8 zurueck.
    [InlineData("4k3/8/8/3b4/8/8/8/4K3 b - - 0 1", "d5f7", true)]
    // Bauern ziehen nie zurueck; Schlaege zaehlen nicht als Rueckzug.
    [InlineData("4k3/8/8/8/4N3/8/2p5/4K3 w - - 0 1", "e4c3", true)]
    [InlineData("4k3/8/8/8/4N3/2p5/8/4K3 w - - 0 1", "e4c3", false)]
    public void Rueckzug_Figur_zieht_ohne_Schlag_Richtung_eigene_Grundreihe(string fen, string uci, bool erwartet)
    {
        Zugmerkmale.Rueckzug(fen, uci).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(Grundstellung, "g1f3", true)]
    [InlineData(Grundstellung, "b1c3", true)]
    [InlineData(Grundstellung, "e2e4", false)]
    [InlineData(Italienisch, "e1g1", true)]
    [InlineData(Italienisch, "f3g5", false)]
    [InlineData("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 2", "f8c5", true)]
    public void Entwicklung_Rochade_oder_Leichtfigur_verlaesst_Startfeld(string fen, string uci, bool erwartet)
    {
        Zugmerkmale.Entwicklung(fen, uci).Should().Be(erwartet);
    }

    [Fact]
    public void Bestimme_fasst_alle_Brettmerkmale_zusammen()
    {
        // Lxf7+: Schlag, Schach, Koenigszone; der Laeufer ist vom Koenig angegriffen und ungedeckt.
        Zugmerkmale.Bestimme(Italienisch, "c4f7").Should().Be(
            Zugmerkmal.Schlagzug | Zugmerkmal.Schach | Zugmerkmal.Koenigszone | Zugmerkmal.Riskant);
        Zugmerkmale.Bestimme(Grundstellung, "e2e4").Should().Be(Zugmerkmal.Keine);
    }

    [Fact]
    public void Stilwahl_mit_echten_Merkmalen_ist_deterministisch()
    {
        IReadOnlyList<Policyzug> policy =
        [
            new("e1g1", 0.35), new("c4f7", 0.05), new("f3g5", 0.25), new("d2d3", 0.20), new("b1c3", 0.15),
        ];
        var aggressiv = new Stilparameter(Stilgewichte.Fuer(Persoenlichkeit.Aggressiv), 1.0);
        Zugmerkmal Merkmale(string uci) => Zugmerkmale.Bestimme(Italienisch, uci);

        var erste = Enumerable.Range(0, 30).Select(h => Stilgewichtung.Waehle(policy, aggressiv, Merkmale, Remislage.Keine, 2026, h)).ToList();
        var zweite = Enumerable.Range(0, 30).Select(h => Stilgewichtung.Waehle(policy, aggressiv, Merkmale, Remislage.Keine, 2026, h)).ToList();

        zweite.Should().Equal(erste);
        erste.Select(w => w.Uci).Distinct().Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Ungewoehnlich_unter_halbem_p_max()
    {
        Zugmerkmale.Ungewoehnlich(p: 0.19, pMax: 0.4).Should().BeTrue();
        Zugmerkmale.Ungewoehnlich(p: 0.2, pMax: 0.4).Should().BeFalse();
    }

    [Theory]
    [InlineData(Grundstellung, 0)]
    // Weiss am Zug, hat eine Dame mehr.
    [InlineData("4k3/8/8/8/8/8/8/3QK3 w - - 0 1", 9)]
    // Schwarz am Zug, Weiss hat Turm mehr: aus Sicht Schwarz -5.
    [InlineData("4k3/8/8/8/8/8/8/3RK3 b - - 0 1", -5)]
    // L + S + B gegen T: 3 + 3 + 1 - 5 = 2.
    [InlineData("4k3/8/8/8/8/8/1rP5/BN2K3 w - - 0 1", 2)]
    public void Materialbilanz_aus_Sicht_der_Seite_am_Zug(string fen, int erwartet)
    {
        Materialbilanz.FuerSeiteAmZug(fen).Should().Be(erwartet);
    }
}
