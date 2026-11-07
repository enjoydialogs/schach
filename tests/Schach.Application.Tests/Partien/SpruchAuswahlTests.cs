using Schach.Application.Partien;
using Schach.Application.Sprueche;
using Schach.Domain;
using Schach.Domain.Entitaeten;

namespace Schach.Application.Tests.Partien;

/// <summary>Ereignisse, Stufen und Abstaende der Sprueche (SCHACH-SPEC 6.1, 6.2).</summary>
public sealed class SpruchAuswahlTests
{
    private static readonly SpruchOptions Optionen = new();

    /// <summary>Spielt die Zuege ab der Grundstellung; der Spieler hat Weiss.</summary>
    internal static List<Zug> Folge(params string[] uci)
    {
        var stellung = Stellung.AusFen(SchachDomain.GrundstellungFen);
        var folge = new List<Zug>();
        for (var i = 0; i < uci.Length; i++)
        {
            var san = stellung.San(uci[i]);
            stellung = stellung.Ziehe(uci[i]);
            folge.Add(new Zug
            {
                Halbzug = i + 1,
                Seite = i % 2 == 0 ? Farbe.Weiss : Farbe.Schwarz,
                Uci = uci[i],
                San = san,
                FenDanach = stellung.Fen,
            });
        }

        return folge;
    }

    private static Spruch S(int nach, SpruchEreignis e) => new() { NachHalbzug = nach, Ereignis = e, Text = e.ToString() };

    private static Gegnerzuglage Lage(int delta = 0, bool gibt = false, bool erhielt = false, bool beide = false, double? vorher = null, double? jetzt = null) =>
        new(delta, gibt, erhielt, beide, vorher, jetzt);

    [Theory]
    [InlineData(5, SpruchEreignis.FigurGewonnen)]
    [InlineData(-9, SpruchEreignis.FigurVerloren)]
    public void Figur_mit_grossem_Delta_ist_Pflicht_ohne_Abstand_und_Deckel(int delta, SpruchEreignis erwartet)
    {
        SpruchAuswahl.NachGegnerzug(Lage(delta), gegnerzuegeSeitSpruch: 0, nichtPflichtBisher: 99, Optionen).Should().Be(erwartet);
    }

    [Fact]
    public void Stufe_B_braucht_zwei_Gegnerzuege_Abstand()
    {
        SpruchAuswahl.NachGegnerzug(Lage(gibt: true), 1, 0, Optionen).Should().BeNull();
        SpruchAuswahl.NachGegnerzug(Lage(gibt: true), 2, 0, Optionen).Should().Be(SpruchEreignis.SchachGegeben);
    }

    [Fact]
    public void Stufe_B_Vorrang()
    {
        SpruchAuswahl.NachGegnerzug(Lage(3, gibt: true, erhielt: true), 2, 0, Optionen).Should().Be(SpruchEreignis.SchachGegeben);
        SpruchAuswahl.NachGegnerzug(Lage(3, erhielt: true), 2, 0, Optionen).Should().Be(SpruchEreignis.FigurGewonnen);
        SpruchAuswahl.NachGegnerzug(Lage(-2, erhielt: true), 2, 0, Optionen).Should().Be(SpruchEreignis.FigurVerloren);
        SpruchAuswahl.NachGegnerzug(Lage(1, erhielt: true, vorher: 0.2, jetzt: 0.9), 2, 0, Optionen).Should().Be(SpruchEreignis.SchachErhalten);
        SpruchAuswahl.NachGegnerzug(Lage(vorher: 0.2, jetzt: 0.45), 2, 0, Optionen).Should().Be(SpruchEreignis.StellungBesser);
        SpruchAuswahl.NachGegnerzug(Lage(vorher: 0.7, jetzt: 0.4), 2, 0, Optionen).Should().Be(SpruchEreignis.StellungSchlechter);
        SpruchAuswahl.NachGegnerzug(Lage(vorher: 0.7, jetzt: 0.5), 2, 0, Optionen).Should().BeNull();
    }

    [Fact]
    public void Bewertungssprung_ist_abschaltbar()
    {
        var aus = new SpruchOptions { Bewertungssprung = false };
        SpruchAuswahl.NachGegnerzug(Lage(vorher: 0.2, jetzt: 0.9), 2, 0, aus).Should().BeNull();
    }

    [Fact]
    public void Stufe_C_nach_vier_Gegnerzuegen_Tausch_vor_Zug()
    {
        SpruchAuswahl.NachGegnerzug(Lage(), 3, 0, Optionen).Should().BeNull();
        SpruchAuswahl.NachGegnerzug(Lage(), 4, 0, Optionen).Should().Be(SpruchEreignis.Zug);
        SpruchAuswahl.NachGegnerzug(Lage(1, beide: true), 4, 0, Optionen).Should().Be(SpruchEreignis.Tausch);
        SpruchAuswahl.NachGegnerzug(Lage(0, beide: true), 4, 0, Optionen).Should().Be(SpruchEreignis.Tausch);
    }

    [Fact]
    public void Ueber_dem_Deckel_nur_noch_Pflicht()
    {
        SpruchAuswahl.NachGegnerzug(Lage(gibt: true), 9, 20, Optionen).Should().BeNull();
        SpruchAuswahl.NachGegnerzug(Lage(6), 9, 20, Optionen).Should().Be(SpruchEreignis.FigurGewonnen);
    }

    [Theory]
    [InlineData(Ergebnis.WeissGewinnt, Farbe.Weiss, SpruchEreignis.Niederlage)]
    [InlineData(Ergebnis.WeissGewinnt, Farbe.Schwarz, SpruchEreignis.Sieg)]
    [InlineData(Ergebnis.Remis, Farbe.Weiss, SpruchEreignis.Remis)]
    public void Partieende_aus_Sicht_des_Gegners(Ergebnis ergebnis, Farbe spieler, SpruchEreignis erwartet)
    {
        SpruchAuswahl.BeiEnde(ergebnis, spieler).Should().Be(erwartet);
    }

    [Fact]
    public void Finn_und_Sementa_reden_mit_kuerzeren_Abstaenden()
    {
        foreach (var key in new[] { "finn", "sementa" })
        {
            var optionen = Optionen.FuerGegner(key);
            SpruchAuswahl.NachGegnerzug(Lage(gibt: true), 1, 0, optionen).Should().Be(SpruchEreignis.SchachGegeben, key);
            SpruchAuswahl.NachGegnerzug(Lage(), 1, 0, optionen).Should().BeNull(key);
            SpruchAuswahl.NachGegnerzug(Lage(), 2, 0, optionen).Should().Be(SpruchEreignis.Zug, key);
        }
    }

    [Fact]
    public void Andere_Gegner_behalten_die_konfigurierten_Abstaende()
    {
        var konfiguriert = new SpruchOptions { AbstandTierB = 3, AbstandTierC = 5, MaxJePartie = 7 };

        var ilse = konfiguriert.FuerGegner("ilse");
        ilse.AbstandTierB.Should().Be(3);
        ilse.AbstandTierC.Should().Be(5);
        konfiguriert.FuerGegner("unbekannt").AbstandTierB.Should().Be(3);

        // Nur die Abstaende aendern sich, Deckel bleiben (Spec 6.2).
        var finn = konfiguriert.FuerGegner("finn");
        (finn.AbstandTierB, finn.AbstandTierC, finn.MaxJePartie).Should().Be((1, 2, 7));
        konfiguriert.AbstandTierB.Should().Be(3);
    }

    [Fact]
    public void Zaehler_aus_den_gespeicherten_Spruechen()
    {
        var folge = Folge("e2e4", "e7e5", "g1f3", "b8c6", "f1c4", "g8f6");
        var sprueche = new List<Spruch> { S(0, SpruchEreignis.Partiebeginn), S(2, SpruchEreignis.Zug), S(2, SpruchEreignis.Zuruecknahme) };

        SpruchAuswahl.GegnerzuegeSeitSpruch(folge, sprueche, Farbe.Weiss).Should().Be(2);
        SpruchAuswahl.GegnerzuegeSeitSpruch(folge, [], Farbe.Weiss).Should().Be(3);
        SpruchAuswahl.NichtPflicht(sprueche).Should().Be(1);
    }

    [Fact]
    public void Erste_Zuruecknahme_ist_Pflicht_weitere_brauchen_Abstand()
    {
        var folge = Folge("e2e4", "e7e5");
        List<Spruch> sprueche = [S(0, SpruchEreignis.Partiebeginn)];

        SpruchAuswahl.NachZuruecknahme(sprueche, folge, Farbe.Weiss, schonZurueckgenommen: false, Optionen).Should().Be(SpruchEreignis.Zuruecknahme);
        SpruchAuswahl.NachZuruecknahme(sprueche, folge, Farbe.Weiss, schonZurueckgenommen: true, Optionen).Should().BeNull();
        SpruchAuswahl.NachZuruecknahme(sprueche, Folge("e2e4", "e7e5", "g1f3", "b8c6"), Farbe.Weiss, true, Optionen)
            .Should().Be(SpruchEreignis.Zuruecknahme);
    }

    [Fact]
    public void Lage_misst_Material_ab_vor_dem_Spielerzug()
    {
        // 1. e4 d5 2. exd5 Dxd5: Bauer gegen Bauer, beide schlagen.
        var tausch = Spruchbau.Lage(Folge("e2e4", "d7d5", "e4d5", "d8d5"), Farbe.Weiss);
        tausch.MaterialAenderung.Should().Be(0);
        tausch.BeideSchlagen.Should().BeTrue();
        tausch.GegnerGibtSchach.Should().BeFalse();

        // 1. e4 e5 2. Dh5 Sc6 3. Dxf7+?? Kxf7: der Gegner gewinnt die Dame gegen einen Bauern.
        var dame = Spruchbau.Lage(Folge("e2e4", "e7e5", "d1h5", "b8c6", "h5f7", "e8f7"), Farbe.Weiss);
        dame.MaterialAenderung.Should().Be(8);
        dame.SpielerGabSchach.Should().BeTrue();
    }

    [Fact]
    public void Anfrage_traegt_Daten_und_Ersatz_Figur()
    {
        var folge = Folge("e2e4", "e7e5", "d1h5", "b8c6", "h5f7", "e8f7");
        var partie = new Partie { Id = Guid.NewGuid(), GegnerId = "finn", SpielerFarbe = Farbe.Weiss, Fen = folge[^1].FenDanach, Seed = 99 };

        var anfrage = Spruchbau.Anfrage(partie, folge, [S(0, SpruchEreignis.Partiebeginn)], SpruchEreignis.FigurGewonnen, 6, null, nurErsatz: false);

        anfrage.GegnerName.Should().Be("Finn");
        anfrage.Werte.Figur.Should().Be('Q');
        anfrage.Daten.MaterialAenderung.Should().Be(8);
        anfrage.Daten.MaterialBilanz.Should().Be(8);
        anfrage.Daten.SpielerZug.Should().Be("Qxf7+");
        anfrage.Daten.GegnerZug.Should().Be("Kxf7");
        anfrage.Daten.SpielerSchlag.Should().Be("Dame des Spielers schlägt Bauer des Gegners");
        anfrage.Daten.GegnerSchlag.Should().Be("König des Gegners schlägt Dame des Spielers");
        anfrage.Daten.GegnerZugMerkmale.Should().Contain("Schlagzug");
        anfrage.Daten.Zugnummer.Should().Be(4);
        anfrage.BisherigeTexte.Should().Equal("Partiebeginn");
    }

    [Fact]
    public void Anfrage_nennt_wer_wen_schlug()
    {
        // 1. e4 d6 2. Dg4?? Lxg4: der Läufer des Gegners schlägt die Dame des Spielers.
        var folge = Folge("e2e4", "d7d6", "d1g4", "c8g4");
        var partie = new Partie { Id = Guid.NewGuid(), GegnerId = "konrad", SpielerFarbe = Farbe.Weiss, Fen = folge[^1].FenDanach, Seed = 1 };

        var anfrage = Spruchbau.Anfrage(partie, folge, [], SpruchEreignis.FigurGewonnen, 4, null, nurErsatz: false);

        anfrage.Daten.GegnerSchlag.Should().Be("Läufer des Gegners schlägt Dame des Spielers");
        anfrage.Daten.SpielerSchlag.Should().BeNull();
        anfrage.Werte.Figur.Should().Be('Q');
    }

    [Fact]
    public void Anfrage_nennt_en_passant_als_Bauernschlag()
    {
        // 1. a3 d5 2. a4 d4 3. e4 dxe3 e.p.
        var folge = Folge("a2a3", "d7d5", "a3a4", "d5d4", "e2e4", "d4e3");
        var partie = new Partie { Id = Guid.NewGuid(), GegnerId = "konrad", SpielerFarbe = Farbe.Weiss, Fen = folge[^1].FenDanach, Seed = 1 };

        var anfrage = Spruchbau.Anfrage(partie, folge, [], SpruchEreignis.Zug, 6, null, nurErsatz: false);

        anfrage.Daten.GegnerSchlag.Should().Be("Bauer des Gegners schlägt Bauer des Spielers");
    }

    [Fact]
    public void Anfrage_traegt_die_Laune_des_Gegners_zum_Halbzug()
    {
        var folge = Folge("e2e4", "e7e5");
        var finn = GegnerKatalog.Finde("finn")!;
        var launen = new HashSet<string>();
        for (var seed = 0; seed < 40; seed++)
        {
            var partie = new Partie { Id = Guid.NewGuid(), GegnerId = "finn", SpielerFarbe = Farbe.Weiss, Fen = folge[^1].FenDanach, Seed = seed };
            var anfrage = Spruchbau.Anfrage(partie, folge, [], SpruchEreignis.Zug, 2, null, nurErsatz: false);

            var erwartet = Launenwahl.Waehle(finn, seed, 2);
            anfrage.Daten.Laune.Should().Be(erwartet.ToString().ToLowerInvariant());
            launen.Add(anfrage.Daten.Laune);
        }

        launen.Should().BeSubsetOf(["trocken", "lustig", "vorlaut", "genervt"]).And.HaveCountGreaterThan(1);
    }
}
