namespace Schach.Domain.Tests;

/// <summary>Elo nach SCHACH-SPEC 3.7: K = 32, int, Untergrenze 100. Erwartete Werte von Hand gerechnet.</summary>
public sealed class EloTests
{
    [Theory]
    // Gleich stark: E = 0,5. Sieg +16, Remis 0, Niederlage -16.
    [InlineData(800, 800, Partieausgang.Sieg, 816)]
    [InlineData(800, 800, Partieausgang.Remis, 800)]
    [InlineData(800, 800, Partieausgang.Niederlage, 784)]
    // 800 gegen 1200: E = 1/(1+10^1) = 0,0909. Sieg: 32*0,9091 = 29,09 -> +29. Niederlage: -2,91 -> -3.
    [InlineData(800, 1200, Partieausgang.Sieg, 829)]
    [InlineData(800, 1200, Partieausgang.Niederlage, 797)]
    // Remis: 32*(0,5-0,0909) = 13,09 -> +13.
    [InlineData(800, 1200, Partieausgang.Remis, 813)]
    // 1200 gegen 800: E = 0,9091. Remis: 32*(-0,4091) = -13,09 -> -13.
    [InlineData(1200, 800, Partieausgang.Remis, 1187)]
    // 2200 gegen 600: E ~ 0,99990. Sieg: 32*0,0001 = 0,003 -> 0.
    [InlineData(2200, 600, Partieausgang.Sieg, 2200)]
    public void Neue_Elo_nach_Standardformel(int alt, int gegner, Partieausgang ausgang, int erwartet)
    {
        Elo.Neu(alt, gegner, ausgang).Should().Be(erwartet);
    }

    [Fact]
    public void Untergrenze_ist_100()
    {
        // 105 gegen 600 verliert: E ~ 0,0549, delta = -1,76 -> -2: 103. 101 verliert: 99 -> 100.
        Elo.Neu(105, 600, Partieausgang.Niederlage).Should().Be(103);
        Elo.Neu(101, 600, Partieausgang.Niederlage).Should().Be(100);
        Elo.Neu(100, 2200, Partieausgang.Niederlage).Should().Be(100);
    }

    [Fact]
    public void Halbe_Punkte_runden_vom_Nullpunkt_weg()
    {
        Elo.Runde(16.5).Should().Be(17);
        Elo.Runde(-16.5).Should().Be(-17);
        Elo.Runde(2.49).Should().Be(2);
    }

    [Theory]
    [InlineData(12, "+12")]
    [InlineData(-8, "−8")]
    [InlineData(0, "±0")]
    public void Anzeige_der_Aenderung_mit_Vorzeichen(int delta, string erwartet)
    {
        Elo.FormatiereAenderung(delta).Should().Be(erwartet);
    }
}
