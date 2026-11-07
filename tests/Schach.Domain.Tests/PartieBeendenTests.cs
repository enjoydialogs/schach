using Microsoft.Extensions.Time.Testing;
using Schach.Domain.Entitaeten;

namespace Schach.Domain.Tests;

/// <summary>Domänenweg <c>Partie.Beenden(ergebnis, grund)</c> nach SCHACH-SPEC 8.11 und 3.7, 3.8.</summary>
public sealed class PartieBeendenTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 7, 18, 30, 0, TimeSpan.Zero);
    private static readonly Guid SpielerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly FakeTimeProvider _zeit = new(Jetzt);

    [Fact]
    public void Setzt_Status_Ergebnis_Grund_und_BeendetAm()
    {
        var partie = SpielerPartie("theo", Farbe.Weiss);

        partie.Beenden(Ergebnis.SchwarzGewinnt, Grund.Matt, _zeit, Spieler(800), LeereBilanz("theo"));

        partie.Status.Should().Be(PartieStatus.Beendet);
        partie.Ergebnis.Should().Be(Ergebnis.SchwarzGewinnt);
        partie.Grund.Should().Be(Grund.Matt);
        partie.BeendetAm.Should().Be(Jetzt);
    }

    [Fact]
    public void Zaehlende_Partie_bucht_die_Elo()
    {
        // 800 gegen Oscar (1000): E = 1/(1+10^0,5) = 0,2403; Sieg: 32*0,7597 = 24,3 -> +24.
        var partie = SpielerPartie("oscar", Farbe.Schwarz);
        var spieler = Spieler(800);

        partie.Beenden(Ergebnis.SchwarzGewinnt, Grund.Aufgabe, _zeit, spieler, LeereBilanz("oscar"));

        partie.EloVorher.Should().Be(800);
        partie.EloNachher.Should().Be(824);
        spieler.Elo.Should().Be(824);
    }

    [Fact]
    public void Remis_gegen_schwaecheren_Gegner_kostet_Elo()
    {
        // 1200 gegen Finn (800): E = 0,9091; Remis: 32*(-0,4091) = -13,1 -> -13.
        var partie = SpielerPartie("finn", Farbe.Weiss);
        var spieler = Spieler(1200);

        partie.Beenden(Ergebnis.Remis, Grund.Wiederholung, _zeit, spieler, LeereBilanz("finn"));

        spieler.Elo.Should().Be(1187);
    }

    [Fact]
    public void Nicht_zaehlende_Partie_laesst_die_Elo_und_bucht_trotzdem_die_Bilanz()
    {
        var partie = SpielerPartie("nele", Farbe.Weiss);
        partie.ZaehltFuerElo = false;
        var spieler = Spieler(950);
        var bilanz = LeereBilanz("nele");

        partie.Beenden(Ergebnis.WeissGewinnt, Grund.Matt, _zeit, spieler, bilanz);

        spieler.Elo.Should().Be(950);
        partie.EloVorher.Should().BeNull();
        partie.EloNachher.Should().BeNull();
        bilanz.Siege.Should().Be(1);
        // Besiegt nur nach einem Sieg, der für die Elo zählt.
        bilanz.BesiegtAm.Should().BeNull();
    }

    [Theory]
    [InlineData(Farbe.Weiss, Ergebnis.WeissGewinnt, 1, 0, 0)]
    [InlineData(Farbe.Schwarz, Ergebnis.WeissGewinnt, 0, 0, 1)]
    [InlineData(Farbe.Schwarz, Ergebnis.Remis, 0, 1, 0)]
    public void Bilanz_zaehlt_aus_Sicht_des_Spielers(Farbe farbe, Ergebnis ergebnis, int siege, int remis, int niederlagen)
    {
        var partie = SpielerPartie("konrad", farbe);
        var bilanz = new Bilanz { SpielerId = SpielerId, GegnerId = "konrad", Siege = 2, Remis = 3, Niederlagen = 4 };

        partie.Beenden(ergebnis, Grund.Vereinbarung, _zeit, Spieler(800), bilanz);

        bilanz.Siege.Should().Be(2 + siege);
        bilanz.Remis.Should().Be(3 + remis);
        bilanz.Niederlagen.Should().Be(4 + niederlagen);
    }

    [Fact]
    public void Erster_zaehlender_Sieg_setzt_BesiegtAm()
    {
        var bilanz = LeereBilanz("ilse");

        SpielerPartie("ilse", Farbe.Weiss).Beenden(Ergebnis.WeissGewinnt, Grund.Matt, _zeit, Spieler(800), bilanz);

        bilanz.BesiegtAm.Should().Be(Jetzt);
    }

    [Fact]
    public void Spaeterer_Sieg_behaelt_das_erste_BesiegtAm()
    {
        var frueher = Jetzt.AddDays(-3);
        var bilanz = new Bilanz { SpielerId = SpielerId, GegnerId = "ilse", Siege = 1, BesiegtAm = frueher };

        SpielerPartie("ilse", Farbe.Weiss).Beenden(Ergebnis.WeissGewinnt, Grund.Matt, _zeit, Spieler(800), bilanz);

        bilanz.BesiegtAm.Should().Be(frueher);
    }

    [Fact]
    public void Niederlage_setzt_kein_BesiegtAm()
    {
        var bilanz = LeereBilanz("maren");

        SpielerPartie("maren", Farbe.Weiss).Beenden(Ergebnis.SchwarzGewinnt, Grund.Zeit, _zeit, Spieler(800), bilanz);

        bilanz.BesiegtAm.Should().BeNull();
    }

    [Fact]
    public void Gastpartie_wird_zum_Loeschen_markiert_ohne_Elo_und_Bilanz()
    {
        var partie = new Partie { Id = Guid.NewGuid(), GegnerId = "finn", SpielerFarbe = Farbe.Weiss, ZaehltFuerElo = false };

        var ende = partie.Beenden(Ergebnis.WeissGewinnt, Grund.Matt, _zeit, spieler: null, bilanz: null);

        ende.Loeschen.Should().BeTrue();
        partie.Status.Should().Be(PartieStatus.Beendet);
        partie.EloVorher.Should().BeNull();
    }

    [Fact]
    public void Spielerpartie_wird_nicht_geloescht()
    {
        var ende = SpielerPartie("finn", Farbe.Weiss).Beenden(Ergebnis.Remis, Grund.Patt, _zeit, Spieler(800), LeereBilanz("finn"));

        ende.Loeschen.Should().BeFalse();
    }

    [Fact]
    public void Ergebnis_ist_endgueltig()
    {
        var partie = SpielerPartie("finn", Farbe.Weiss);
        partie.Beenden(Ergebnis.Remis, Grund.Patt, _zeit, Spieler(800), LeereBilanz("finn"));

        var nochmal = () => partie.Beenden(Ergebnis.WeissGewinnt, Grund.Aufgabe, _zeit, Spieler(800), LeereBilanz("finn"));

        nochmal.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Spielerpartie_braucht_Spieler_und_passende_Bilanz()
    {
        var ohneSpieler = () => SpielerPartie("finn", Farbe.Weiss).Beenden(Ergebnis.Remis, Grund.Patt, _zeit, null, LeereBilanz("finn"));
        var fremdeBilanz = () => SpielerPartie("finn", Farbe.Weiss).Beenden(Ergebnis.Remis, Grund.Patt, _zeit, Spieler(800), LeereBilanz("theo"));

        ohneSpieler.Should().Throw<ArgumentException>();
        fremdeBilanz.Should().Throw<ArgumentException>();
    }

    private static Partie SpielerPartie(string gegner, Farbe farbe) => new()
    {
        Id = Guid.NewGuid(),
        SpielerId = SpielerId,
        GegnerId = gegner,
        SpielerFarbe = farbe,
        Status = PartieStatus.Offen,
        ZaehltFuerElo = true,
    };

    private static Spieler Spieler(int elo) => new() { Id = SpielerId, Elo = elo };

    private static Bilanz LeereBilanz(string gegner) => new() { SpielerId = SpielerId, GegnerId = gegner };
}
