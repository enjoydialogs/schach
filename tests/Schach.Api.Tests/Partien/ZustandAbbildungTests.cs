using Microsoft.Extensions.Time.Testing;
using Schach.Api.Contracts;
using Schach.Api.Controllers;
using Schach.Application.Partien;
using Schach.Domain.Entitaeten;
using Domain = Schach.Domain;

namespace Schach.Api.Tests.Partien;

/// <summary>Zustand-DTO aus der Domaene (SCHACH-SPEC 8.4, 8.4a), ohne Datenbank.</summary>
public sealed class ZustandAbbildungTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 7, 18, 30, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _zeit = new(Jetzt);

    public static TheoryData<Type, Type> EnumPaare() => new()
    {
        { typeof(Domain.Farbe), typeof(Contracts.Farbe) },
        { typeof(Domain.Ergebnis), typeof(Contracts.Ergebnis) },
        { typeof(Domain.Grund), typeof(Contracts.Grund) },
        { typeof(Domain.Entitaeten.Bedenkzeit), typeof(Contracts.Bedenkzeit) },
        { typeof(Domain.Entitaeten.PartieStatus), typeof(Contracts.PartieStatus) },
        { typeof(Domain.Entitaeten.SpruchEreignis), typeof(Contracts.SpruchEreignis) },
        { typeof(Domain.UhrSeite), typeof(Contracts.UhrSeite) },
        { typeof(Domain.Stufe), typeof(Contracts.Stufe) },
        { typeof(Domain.Persoenlichkeit), typeof(Contracts.Persoenlichkeit) },
    };

    [Theory]
    [MemberData(nameof(EnumPaare))]
    public void Domaenen_und_Wire_Enums_tragen_dieselben_Namen(Type domaene, Type wire)
    {
        Enum.GetNames(domaene).Should().Equal(Enum.GetNames(wire));
    }

    [Fact]
    public void Spieler_am_Zug_mit_Uhr()
    {
        var partie = Partiefuehrung.Starte(Guid.NewGuid(), null, Domain.GegnerKatalog.Finde("theo")!, Domain.Farbe.Weiss, Domain.Entitaeten.Bedenkzeit.Fischer5Plus3, 7, _zeit);
        _zeit.Advance(TimeSpan.FromSeconds(12));

        var zustand = ZustandAbbildung.Zustand(new PartieSicht(partie, [], [], null), _zeit);

        zustand.Status.Should().Be(Contracts.PartieStatus.Offen);
        zustand.SpielerFarbe.Should().Be(Contracts.Farbe.Weiss);
        zustand.Gegner.Should().BeEquivalentTo(new { Key = "theo", Name = "Theo", Elo = 1000, Stufe = Contracts.Stufe.Hobbyspieler, Bilanz = (BilanzDto?)null });
        zustand.Bedenkzeit.Should().Be(Contracts.Bedenkzeit.Fischer5Plus3);
        zustand.Halbzug.Should().Be(0);
        zustand.ErlaubteZuege.Should().HaveCount(20).And.Contain("e2e4");
        zustand.AmZug.Should().BeTrue();
        zustand.GegnerZugAusstehend.Should().BeFalse();
        zustand.GegnerZug.Should().BeNull();
        zustand.Uhr.Should().Be(new UhrDto(288_000, 300_000, Contracts.UhrSeite.Spieler, Jetzt, Jetzt.AddSeconds(12)));
        zustand.ZaehltFuerElo.Should().BeFalse();
        zustand.RemisAngebotMoeglichAbZug.Should().Be(1);
        zustand.Pgn.Should().BeNull();
        zustand.Ergebnis.Should().BeNull();
    }

    [Fact]
    public void Gegnerzug_ist_ab_gespieltAm_sichtbar_und_ausstehend_hat_keine_erlaubten_Zuege()
    {
        var partie = Partiefuehrung.Starte(Guid.NewGuid(), null, Domain.GegnerKatalog.Finde("ilse")!, Domain.Farbe.Schwarz, Domain.Entitaeten.Bedenkzeit.OhneUhr, 7, _zeit);

        var ausstehend = ZustandAbbildung.Zustand(new PartieSicht(partie, [], [], null), _zeit);
        ausstehend.AmZug.Should().BeFalse();
        ausstehend.GegnerZugAusstehend.Should().BeTrue();
        ausstehend.ErlaubteZuege.Should().BeEmpty();
        ausstehend.Uhr.Should().BeNull();

        var zug = partie.SpieleGegnerzug([], new Gegnerwahl("e2e4", ZugEngine.Maia, 1, 1, 1, null), _zeit).Zug!;
        var zustand = ZustandAbbildung.Zustand(new PartieSicht(partie, [zug], [], zug), _zeit);

        zustand.GegnerZug.Should().Be(new GegnerZugDto(1, Contracts.Farbe.Weiss, "e2e4", "e4", zug.GespieltAm, zug.GespieltAm, null));
        zustand.Zugfolge.Should().ContainSingle().Which.Should().Be(new ZugDto(1, Contracts.Farbe.Weiss, "e2e4", "e4", zug.GespieltAm));
        zustand.AmZug.Should().BeTrue();
    }

    [Fact]
    public void Beendete_Partie_traegt_Ergebnis_Grund_und_PGN()
    {
        var partie = Partiefuehrung.Starte(Guid.NewGuid(), null, Domain.GegnerKatalog.Finde("ilse")!, Domain.Farbe.Weiss, Domain.Entitaeten.Bedenkzeit.OhneUhr, 7, _zeit);
        var zug = partie.SpieleSpielerzug([], "e2e4", _zeit).Zug!;
        partie.Beenden(Domain.Ergebnis.SchwarzGewinnt, Domain.Grund.Aufgabe, _zeit, null, null);

        var zustand = ZustandAbbildung.Zustand(new PartieSicht(partie, [zug], [], null), _zeit);

        zustand.Status.Should().Be(Contracts.PartieStatus.Beendet);
        zustand.Ergebnis.Should().Be(Contracts.Ergebnis.SchwarzGewinnt);
        zustand.Grund.Should().Be(Contracts.Grund.Aufgabe);
        zustand.AmZug.Should().BeFalse();
        zustand.ErlaubteZuege.Should().BeEmpty();
        zustand.Pgn.Should().Contain("[Termination \"Aufgabe\"]").And.Contain("1. e4 0-1");
    }
}
