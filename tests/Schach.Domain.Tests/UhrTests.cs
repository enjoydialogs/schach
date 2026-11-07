using Microsoft.Extensions.Time.Testing;

namespace Schach.Domain.Tests;

/// <summary>Uhr nach SCHACH-SPEC 3.2 und 8.6, gefahren über einen <see cref="FakeTimeProvider"/>.</summary>
public sealed class UhrTests
{
    private static readonly DateTimeOffset Beginn = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ohne_Uhr_gibt_es_keine_Uhr()
    {
        PartieUhr.Starte(Bedenkzeit.OhneUhr, UhrSeite.Spieler, new FakeTimeProvider(Beginn)).Should().BeNull();
    }

    [Theory]
    [InlineData(Bedenkzeit.Fischer15Plus10, 900_000)]
    [InlineData(Bedenkzeit.Fischer10Plus5, 600_000)]
    [InlineData(Bedenkzeit.Fischer5Plus3, 300_000)]
    [InlineData(Bedenkzeit.JeZug90s, 90_000)]
    public void Uhr_startet_bei_Erstellung_mit_voller_Zeit(Bedenkzeit bedenkzeit, long erwartetMs)
    {
        var zeit = new FakeTimeProvider(Beginn);

        var uhr = PartieUhr.Starte(bedenkzeit, UhrSeite.Spieler, zeit)!;

        uhr.SpielerRestMs.Should().Be(erwartetMs);
        uhr.GegnerRestMs.Should().Be(erwartetMs);
        uhr.LaeuftFuer.Should().Be(UhrSeite.Spieler);
        uhr.AmZugSeit.Should().Be(Beginn);
    }

    [Fact]
    public void Laufende_Seite_verliert_Zeit_auch_ohne_Zug()
    {
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.Fischer5Plus3, UhrSeite.Spieler, zeit)!;

        zeit.Advance(TimeSpan.FromSeconds(42));

        uhr.RestMs(UhrSeite.Spieler, zeit).Should().Be(258_000);
        uhr.RestMs(UhrSeite.Gegner, zeit).Should().Be(300_000);
    }

    [Fact]
    public void Fischer_Inkrement_kommt_nach_dem_Zug_dazu()
    {
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.Fischer15Plus10, UhrSeite.Spieler, zeit)!;

        zeit.Advance(TimeSpan.FromSeconds(30));
        uhr = uhr.ZugGespielt(zeit);

        // 900 s - 30 s + 10 s.
        uhr.SpielerRestMs.Should().Be(880_000);
        uhr.GegnerRestMs.Should().Be(900_000);
        uhr.LaeuftFuer.Should().Be(UhrSeite.Gegner);
        uhr.AmZugSeit.Should().Be(Beginn.AddSeconds(30));
    }

    [Fact]
    public void Inkrement_kann_ueber_die_Startzeit_hinaus_wachsen()
    {
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.Fischer10Plus5, UhrSeite.Spieler, zeit)!;

        zeit.Advance(TimeSpan.FromSeconds(1));
        uhr = uhr.ZugGespielt(zeit);

        uhr.SpielerRestMs.Should().Be(604_000);
    }

    [Fact]
    public void Neunzig_Sekunden_je_Zug_ohne_Uebertrag()
    {
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.JeZug90s, UhrSeite.Spieler, zeit)!;

        zeit.Advance(TimeSpan.FromSeconds(10));
        uhr = uhr.ZugGespielt(zeit);
        // Kein Übertrag der 80 s: der Spieler hat beim nächsten Zug wieder genau 90 s.
        uhr.SpielerRestMs.Should().Be(90_000);

        zeit.Advance(TimeSpan.FromSeconds(85));
        uhr = uhr.ZugGespielt(zeit);
        uhr.GegnerRestMs.Should().Be(90_000);
        uhr.LaeuftFuer.Should().Be(UhrSeite.Spieler);
        uhr.RestMs(UhrSeite.Spieler, zeit).Should().Be(90_000);
    }

    [Fact]
    public void Ablauf_wird_festgestellt_wenn_die_Restzeit_verbraucht_ist()
    {
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.JeZug90s, UhrSeite.Spieler, zeit)!;

        zeit.Advance(TimeSpan.FromMilliseconds(89_999));
        uhr.IstAbgelaufen(zeit).Should().BeFalse();

        zeit.Advance(TimeSpan.FromMilliseconds(1));
        uhr.IstAbgelaufen(zeit).Should().BeTrue();
        uhr.AblaufAm.Should().Be(Beginn.AddSeconds(90));
        uhr.RestMs(UhrSeite.Spieler, zeit).Should().Be(0);
    }

    [Fact]
    public void Zug_nach_Ablauf_ist_nicht_erlaubt()
    {
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.Fischer5Plus3, UhrSeite.Spieler, zeit)!;

        zeit.Advance(TimeSpan.FromMinutes(6));

        var zug = () => uhr.ZugGespielt(zeit);
        zug.Should().Throw<UhrAbgelaufenException>();
    }

    [Fact]
    public void Gegnerzug_belastet_die_Zeit_bis_gespieltAm()
    {
        // Spielerzug um 12:00:10 empfangen, D = 2 s, Commit schon nach 0,5 s: gespieltAm = 12:00:12.
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.Fischer5Plus3, UhrSeite.Spieler, zeit)!;
        zeit.Advance(TimeSpan.FromSeconds(10));
        var empfangenAm = zeit.GetUtcNow();
        uhr = uhr.ZugGespielt(zeit);
        zeit.Advance(TimeSpan.FromMilliseconds(500));

        var gespieltAm = PartieUhr.GegnerzugGespieltAm(empfangenAm, 2_000, zeit.GetUtcNow());
        uhr = uhr.GegnerzugGespielt(gespieltAm);

        gespieltAm.Should().Be(empfangenAm.AddSeconds(2));
        uhr.GegnerRestMs.Should().Be(300_000 - 2_000 + 3_000);
        uhr.LaeuftFuer.Should().Be(UhrSeite.Spieler);
        uhr.AmZugSeit.Should().Be(gespieltAm);
    }

    [Fact]
    public void Langsamer_Commit_schiebt_gespieltAm_hinaus()
    {
        var empfangenAm = Beginn;
        PartieUhr.GegnerzugGespieltAm(empfangenAm, 1_500, Beginn.AddSeconds(4)).Should().Be(Beginn.AddSeconds(4));
    }

    [Fact]
    public void Bei_Engine_Ausfall_wird_dem_Gegner_nur_D_belastet()
    {
        var zeit = new FakeTimeProvider(Beginn);
        var uhr = PartieUhr.Starte(Bedenkzeit.Fischer5Plus3, UhrSeite.Gegner, zeit)!;

        // Der Sweep holt den Zug erst 40 s später nach.
        zeit.Advance(TimeSpan.FromSeconds(40));
        uhr = uhr.GegnerzugNachEngineAusfall(1_800, zeit);

        uhr.GegnerRestMs.Should().Be(300_000 - 1_800 + 3_000);
        uhr.LaeuftFuer.Should().Be(UhrSeite.Spieler);
        uhr.AmZugSeit.Should().Be(Beginn.AddSeconds(40));
    }
}
