using Schach.Application.Abstractions;
using Schach.Application.Partien;

namespace Schach.Application.Tests.Partien;

/// <summary>Remis-Angebot nach SCHACH-SPEC 3.5 und 11.2.</summary>
public sealed class RemisangebotTests
{
    private static EngineAnalyseZeile Zeile(int? cp, int? mate = null) => new("e2e4", cp, mate, new EngineWdl(0.3, 0.4, 0.3));

    [Theory]
    [InlineData(1, null, RemisAntwort.AbgelehntVorZug20)]
    [InlineData(19, null, RemisAntwort.AbgelehntVorZug20)]
    [InlineData(16, 10, RemisAntwort.AbgelehntVorZug20)]
    [InlineData(14, 10, RemisAntwort.Gesperrt)]
    [InlineData(24, 20, RemisAntwort.Gesperrt)]
    public void Sperren_gelten_vor_der_Engine(int zugnummer, int? letztes, RemisAntwort erwartet)
    {
        Remisangebot.VorEngine(zugnummer, letztes).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(20, null)]
    [InlineData(25, 20)]
    public void Ab_Zug_20_ohne_Sperre_fragt_sie_Stockfish(int zugnummer, int? letztes)
    {
        Remisangebot.VorEngine(zugnummer, letztes).Should().BeNull();
    }

    [Fact]
    public void Moeglich_ab_ist_das_letzte_Angebot_plus_5()
    {
        Remisangebot.MoeglichAbZug(null).Should().Be(1);
        Remisangebot.MoeglichAbZug(21).Should().Be(26);
    }

    [Theory]
    [InlineData(149, true, RemisAntwort.Angenommen)]
    [InlineData(150, true, RemisAntwort.AbgelehntKlarVorn)]
    [InlineData(-300, true, RemisAntwort.Angenommen)]
    // Spieler am Zug: +200 fuer den Spieler sind -200 fuer den Gegner.
    [InlineData(200, false, RemisAntwort.Angenommen)]
    [InlineData(-200, false, RemisAntwort.AbgelehntKlarVorn)]
    public void Annahme_unter_plus_1_5_aus_Sicht_des_Gegners(int cp, bool gegnerAmZug, RemisAntwort erwartet)
    {
        Remisangebot.NachEngine(Zeile(cp), gegnerAmZug, 0).Should().Be(erwartet);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-3)]
    public void Jedes_Matt_in_Sicht_lehnt_ab(int mate)
    {
        Remisangebot.NachEngine(Zeile(null, mate), gegnerAmZug: true, 0).Should().Be(RemisAntwort.AbgelehntKlarVorn);
    }

    [Theory]
    [InlineData(1, RemisAntwort.Angenommen)]
    [InlineData(-4, RemisAntwort.Angenommen)]
    [InlineData(2, RemisAntwort.AbgelehntKlarVorn)]
    public void Ohne_Engine_entscheidet_das_Material(int material, RemisAntwort erwartet)
    {
        Remisangebot.NachEngine(null, gegnerAmZug: false, material).Should().Be(erwartet);
    }
}
