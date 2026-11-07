using Microsoft.Extensions.Time.Testing;
using Schach.Domain.Entitaeten;
using Schach.Domain.Stil;

namespace Schach.Domain.Tests;

/// <summary>Partie-Kern ohne Datenbank (SCHACH-SPEC 3.1 bis 3.3, 8.6, 8.8): Starten, Spielerzug,
/// Gegnerzug, Einordnen des Konflikttokens, Zuruecknehmen.</summary>
public sealed class PartiefuehrungTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 7, 18, 30, 0, TimeSpan.Zero);
    private static readonly Guid SpielerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Gegner Oscar = GegnerKatalog.Finde("oscar")!;

    private readonly FakeTimeProvider _zeit = new(Jetzt);
    private readonly List<Zug> _zugfolge = [];

    private Partie Neu(Farbe farbe = Farbe.Weiss, Bedenkzeit bedenkzeit = Bedenkzeit.OhneUhr, Guid? spieler = null) =>
        Partiefuehrung.Starte(Guid.NewGuid(), spieler ?? SpielerId, Oscar, farbe, bedenkzeit, seed: 42, _zeit);

    private void Spieler(Partie partie, string uci) => _zugfolge.Add(partie.SpieleSpielerzug(_zugfolge, uci, _zeit).Zug!);

    private void Gegner(Partie partie, string uci) =>
        _zugfolge.Add(partie.SpieleGegnerzug(_zugfolge, new Gegnerwahl(uci, ZugEngine.Maia, 1, 1.0, 1.2, null), _zeit).Zug!);

    [Fact]
    public void Starte_legt_offene_Partie_in_der_Grundstellung_an()
    {
        var partie = Neu(Farbe.Schwarz);

        partie.Status.Should().Be(PartieStatus.Offen);
        partie.Fen.Should().Be(SchachDomain.GrundstellungFen);
        partie.Halbzuege.Should().Be(0);
        partie.SpielerFarbe.Should().Be(Farbe.Schwarz);
        partie.GegnerId.Should().Be("oscar");
        partie.ErstelltAm.Should().Be(Jetzt);
        partie.LetzterZugAm.Should().Be(Jetzt);
        partie.ZaehltFuerElo.Should().BeTrue();
        partie.SpielerRestMs.Should().BeNull();
        partie.AmZugSeit.Should().BeNull();
        partie.SpielerAmZug().Should().BeFalse();
        partie.GegnerZugAusstehend().Should().BeTrue();
    }

    [Fact]
    public void Gastpartie_zaehlt_nicht_fuer_die_Elo()
    {
        var partie = Partiefuehrung.Starte(Guid.NewGuid(), null, Oscar, Farbe.Weiss, Bedenkzeit.OhneUhr, 1, _zeit);

        partie.SpielerId.Should().BeNull();
        partie.ZaehltFuerElo.Should().BeFalse();
    }

    [Fact]
    public void Mit_Bedenkzeit_startet_die_Uhr_bei_Erstellung()
    {
        var partie = Neu(bedenkzeit: Bedenkzeit.Fischer5Plus3);

        partie.SpielerRestMs.Should().Be(300_000);
        partie.GegnerRestMs.Should().Be(300_000);
        partie.AmZugSeit.Should().Be(Jetzt);
    }

    [Fact]
    public void Spielerzug_fuehrt_Zugfolge_Fen_und_Version_fort()
    {
        var partie = Neu();
        _zeit.Advance(TimeSpan.FromSeconds(4));

        var ergebnis = partie.SpieleSpielerzug(_zugfolge, "e2e4", _zeit);

        ergebnis.Ende.Should().BeNull();
        var zug = ergebnis.Zug!;
        zug.Halbzug.Should().Be(1);
        zug.Seite.Should().Be(Farbe.Weiss);
        zug.Uci.Should().Be("e2e4");
        zug.San.Should().Be("e4");
        zug.GespieltAm.Should().Be(Jetzt.AddSeconds(4));
        zug.Engine.Should().BeNull();
        partie.Halbzuege.Should().Be(1);
        partie.Fen.Should().StartWith("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq ");
        zug.FenDanach.Should().Be(partie.Fen);
        partie.LetzterZugAm.Should().Be(Jetzt.AddSeconds(4));
        partie.Version.Should().Be(1);
        partie.GegnerZugAusstehend().Should().BeTrue();
    }

    [Fact]
    public void Unerlaubter_Spielerzug_wirft_und_aendert_nichts()
    {
        var partie = Neu();

        var akt = () => partie.SpieleSpielerzug(_zugfolge, "e2e5", _zeit);

        akt.Should().Throw<ZugUnzulaessigException>();
        partie.Halbzuege.Should().Be(0);
        partie.Version.Should().Be(0);
    }

    [Fact]
    public void Spielerzug_mit_Uhr_zieht_Zeit_ab_und_gibt_Inkrement()
    {
        var partie = Neu(bedenkzeit: Bedenkzeit.Fischer5Plus3);
        _zeit.Advance(TimeSpan.FromSeconds(10));

        Spieler(partie, "e2e4");

        partie.SpielerRestMs.Should().Be(300_000 - 10_000 + 3_000);
        partie.GegnerRestMs.Should().Be(300_000);
        partie.AmZugSeit.Should().Be(Jetzt.AddSeconds(10));
    }

    [Fact]
    public void Spielerzug_nach_Ablauf_der_Uhr_wirft()
    {
        var partie = Neu(bedenkzeit: Bedenkzeit.JeZug90s);
        _zeit.Advance(TimeSpan.FromSeconds(91));

        var akt = () => partie.SpieleSpielerzug(_zugfolge, "e2e4", _zeit);

        akt.Should().Throw<UhrAbgelaufenException>();
        partie.Halbzuege.Should().Be(0);
    }

    [Fact]
    public void Gegnerzug_wird_nach_der_Denkzeit_sichtbar_und_speichert_die_Zugwahl()
    {
        var partie = Neu(bedenkzeit: Bedenkzeit.Fischer15Plus10);
        Spieler(partie, "e2e4");
        var empfangen = _zeit.GetUtcNow();
        var d = Gegnerdenkzeit.Ms(42, 2, partie.GegnerRestMs);

        var ergebnis = partie.SpieleGegnerzug(_zugfolge, new Gegnerwahl("e7e5", ZugEngine.Maia, 2, 0.8, 1.3, new Wdl { W = 0.4, D = 0.3, L = 0.3 }), _zeit);

        var zug = ergebnis.Zug!;
        zug.Halbzug.Should().Be(2);
        zug.Seite.Should().Be(Farbe.Schwarz);
        zug.San.Should().Be("e5");
        zug.Engine.Should().Be(ZugEngine.Maia);
        zug.MaiaRang.Should().Be(2);
        zug.PAnteil.Should().Be(0.8);
        zug.W.Should().Be(1.3);
        zug.Wdl!.W.Should().Be(0.4);
        d.Should().BeInRange(1_000, 3_000);
        zug.GespieltAm.Should().Be(empfangen.AddMilliseconds(d));
        partie.LetzterZugAm.Should().Be(zug.GespieltAm);
        partie.AmZugSeit.Should().Be(zug.GespieltAm);
        partie.GegnerRestMs.Should().Be(900_000 - d + 10_000);
        partie.SpielerAmZug().Should().BeTrue();
        partie.Version.Should().Be(2);
    }

    [Fact]
    public void Gegnerzug_nach_langsamer_Engine_gilt_ab_Commitzeit()
    {
        var partie = Neu();
        Spieler(partie, "e2e4");
        _zeit.Advance(TimeSpan.FromSeconds(5));

        var zug = partie.SpieleGegnerzug(_zugfolge, new Gegnerwahl("e7e5", ZugEngine.Fairy, null, null, null, null), _zeit).Zug!;

        zug.GespieltAm.Should().Be(_zeit.GetUtcNow());
        zug.Engine.Should().Be(ZugEngine.Fairy);
        zug.MaiaRang.Should().BeNull();
    }

    [Fact]
    public void Unerlaubter_Gegnerzug_wirft()
    {
        var partie = Neu();
        Spieler(partie, "e2e4");

        var akt = () => partie.SpieleGegnerzug(_zugfolge, new Gegnerwahl("e2e4", ZugEngine.Maia, 1, 1, 1, null), _zeit);

        akt.Should().Throw<ZugUnzulaessigException>();
    }

    [Fact]
    public void Matt_durch_den_Gegner_meldet_das_Partieende()
    {
        var partie = Neu();
        Spieler(partie, "f2f3");
        Gegner(partie, "e7e5");
        Spieler(partie, "g2g4");

        var ergebnis = partie.SpieleGegnerzug(_zugfolge, new Gegnerwahl("d8h4", ZugEngine.Maia, 1, 1, 1, null), _zeit);

        ergebnis.Ende.Should().Be(new Partieende(Ergebnis.SchwarzGewinnt, Grund.Matt));
        partie.Halbzuege.Should().Be(4);
    }

    [Theory]
    [InlineData(2, "g1f3", Zugeinordnung.Neu)]
    [InlineData(0, "e2e4", Zugeinordnung.Wiederholung)]
    [InlineData(0, "d2d4", Zugeinordnung.HalbzugVeraltet)]
    [InlineData(1, "e7e5", Zugeinordnung.HalbzugVeraltet)]
    [InlineData(3, "g1f3", Zugeinordnung.HalbzugVeraltet)]
    public void Ordnet_das_Konflikttoken_ein(int halbzug, string uci, Zugeinordnung erwartet)
    {
        var partie = Neu();
        Spieler(partie, "e2e4");
        Gegner(partie, "e7e5");

        partie.OrdneEin(_zugfolge, halbzug, uci).Should().Be(erwartet);
    }

    [Fact]
    public void Bei_ausstehendem_Gegnerzug_ist_der_Spieler_nicht_am_Zug()
    {
        var partie = Neu();
        Spieler(partie, "e2e4");

        partie.OrdneEin(_zugfolge, 1, "d2d4").Should().Be(Zugeinordnung.NichtAmZug);
    }

    [Fact]
    public void In_beendeter_Partie_gilt_nur_die_Wiederholung()
    {
        var partie = Neu();
        Spieler(partie, "e2e4");
        partie.Beenden(Ergebnis.SchwarzGewinnt, Grund.Aufgabe, _zeit, new Spieler { Id = SpielerId, Elo = 800 }, new Bilanz { SpielerId = SpielerId, GegnerId = "oscar" });

        partie.OrdneEin(_zugfolge, 0, "e2e4").Should().Be(Zugeinordnung.Wiederholung);
        partie.OrdneEin(_zugfolge, 1, "d7d5").Should().Be(Zugeinordnung.PartieBeendet);
    }

    [Fact]
    public void Zuruecknehmen_entfernt_Spielerzug_und_Gegnerantwort()
    {
        var partie = Neu();
        Spieler(partie, "e2e4");
        Gegner(partie, "e7e5");
        Spieler(partie, "g1f3");
        Gegner(partie, "b8c6");

        var neu = partie.NimmZurueck(_zugfolge);

        neu.Should().Be(2);
        partie.Halbzuege.Should().Be(2);
        partie.Fen.Should().Be(_zugfolge[1].FenDanach);
        partie.ZaehltFuerElo.Should().BeFalse();
        partie.Version.Should().Be(5);
        partie.SpielerAmZug().Should().BeTrue();
    }

    [Fact]
    public void Zuruecknehmen_bei_ausstehendem_Gegnerzug_entfernt_nur_den_Spielerzug()
    {
        var partie = Neu();
        Spieler(partie, "e2e4");

        partie.NimmZurueck(_zugfolge).Should().Be(0);

        partie.Fen.Should().Be(SchachDomain.GrundstellungFen);
    }

    [Fact]
    public void Zuruecknehmen_braucht_einen_eigenen_Zug()
    {
        var partie = Neu(Farbe.Schwarz);
        Gegner(partie, "e2e4");

        var akt = () => partie.NimmZurueck(_zugfolge);

        akt.Should().Throw<ZuruecknehmenNichtErlaubtException>();
    }

    [Fact]
    public void Zuruecknehmen_nur_ohne_Bedenkzeit()
    {
        var partie = Neu(bedenkzeit: Bedenkzeit.Fischer10Plus5);
        Spieler(partie, "e2e4");

        var akt = () => partie.NimmZurueck(_zugfolge);

        akt.Should().Throw<ZuruecknehmenNichtErlaubtException>();
    }

    [Fact]
    public void Gegnerzugwahl_meidet_sofortiges_Remis_wenn_der_Gegner_klar_vorn_steht()
    {
        // Weiss (Gegner) mit Dame mehr: Df7 waere Patt, Df8 ist Matt.
        var verlauf = new List<Stellung> { Stellung.AusFen("7k/8/6K1/8/8/8/8/5Q2 w - - 0 1") };
        var policy = new[] { new Policyzug("f1f7", 0.6), new Policyzug("f1f8", 0.4) };

        var gewaehlt = Enumerable.Range(1, 20)
            .Select(seed => Partiefuehrung.WaehleGegnerzug(verlauf, policy, new Stilparameter(Stilgewichte.Neutral, 1.0), seed, 1).Uci)
            .Distinct();

        gewaehlt.Should().Equal("f1f8");
    }
}
