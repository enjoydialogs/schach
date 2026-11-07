using Schach.Application.Partien;
using Schach.Domain;

namespace Schach.Application.Tests.Partien;

/// <summary>FEN-Helfer und Tipp-Zerlegung (SCHACH-SPEC 3.5, 7.3).</summary>
public sealed class BrettlageUndTippTests
{
    private const string NachE4 = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

    [Fact]
    public void Zugnummer_ist_FEN_Feld_6()
    {
        Brettlage.Zugnummer(SchachDomain.GrundstellungFen).Should().Be(1);
        Brettlage.Zugnummer("8/8/8/8/8/8/8/K6k w - - 0 37").Should().Be(37);
    }

    [Theory]
    [InlineData("e1", 'K')]
    [InlineData("d8", 'q')]
    [InlineData("e4", 'P')]
    [InlineData("e2", null)]
    [InlineData("a8", 'r')]
    [InlineData("h1", 'R')]
    public void Figur_auf_einem_Feld(string feld, char? erwartet)
    {
        Brettlage.FigurAuf(NachE4, feld).Should().Be(erwartet);
    }

    [Fact]
    public void Material_aus_Sicht_einer_Seite()
    {
        const string weissOhneDame = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNB1KBNR b KQkq - 0 1";
        Brettlage.Material(weissOhneDame, Farbe.Schwarz).Should().Be(9);
        Brettlage.Material(weissOhneDame, Farbe.Weiss).Should().Be(-9);
    }

    [Fact]
    public void Phase_nach_Zugnummer_und_Material()
    {
        Brettlage.Phase(SchachDomain.GrundstellungFen).Should().Be("Eröffnung");
        Brettlage.Phase("r1bqkbnr/pppppppp/2n5/8/8/2N5/PPPPPPPP/R1BQKBNR w KQkq - 4 15").Should().Be("Mittelspiel");
        Brettlage.Phase("4k3/8/8/8/8/8/4P3/R3K3 w - - 0 40").Should().Be("Endspiel");
    }

    [Fact]
    public void Tipp_zerlegt_den_Zug_in_Felder_und_Figur()
    {
        Tippzug.Aus(NachE4, "g8f6").Should().Be(new Tippzug("g8f6", "g8", "f6", 'n'));
        Tippzug.Aus("8/4P3/8/8/8/8/8/K6k w - - 0 50", "e7e8q").Should().Be(new Tippzug("e7e8q", "e7", "e8", 'p'));
    }

    [Fact]
    public void Tipp_cache_merkt_je_FEN_und_leert_sich_ueber_der_Grenze()
    {
        var cache = new TippCache(new TippOptions { CacheEintraege = 2 });
        cache.Merke("a", "e2e4");
        cache.Merke("b", "d2d4");

        cache.Versuche("a", out var a).Should().BeTrue();
        a.Should().Be("e2e4");

        cache.Merke("c", "c2c4");
        cache.Versuche("a", out _).Should().BeFalse();
        cache.Versuche("c", out var c).Should().BeTrue();
        c.Should().Be("c2c4");
    }
}
