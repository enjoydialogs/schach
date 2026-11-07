namespace Schach.Domain.Tests;

/// <summary>Gera.Chess-Adapter <see cref="Stellung"/> gegen die FEN-Suite (SCHACH-SPEC 3.4.9, 15.2).</summary>
public sealed class StellungTests
{
    public static TheoryData<string> PerftIds => FenSuite.Ids("perft");

    [Theory]
    [MemberData(nameof(PerftIds))]
    public void Perft_stimmt_mit_der_Suite_ueberein(string id)
    {
        var fall = FenSuite.Fall("perft", id);
        var stellung = Stellung.AusFen(fall.GetProperty("fen").GetString()!);

        foreach (var tiefe in fall.GetProperty("perft").EnumerateObject())
        {
            stellung.Perft(int.Parse(tiefe.Name, System.Globalization.CultureInfo.InvariantCulture))
                .Should().Be(tiefe.Value.GetInt64(), $"{id} Tiefe {tiefe.Name}");
        }
    }

    public static TheoryData<string> ZuegeIds => FenSuite.Ids("zuege");

    [Theory]
    [MemberData(nameof(ZuegeIds))]
    public void Erlaubte_Zuege_als_UCI_stimmen_mit_der_Suite_ueberein(string id)
    {
        var fall = FenSuite.Fall("zuege", id);
        var stellung = Stellung.AusFen(fall.GetProperty("fen").GetString()!).Ziehe(FenSuite.Zugfolge(fall));

        var zuege = stellung.ErlaubteZuege();

        if (fall.TryGetProperty("anzahl", out var anzahl))
        {
            zuege.Should().HaveCount(anzahl.GetInt32(), id);
        }

        if (fall.TryGetProperty("enthaelt", out var enthaelt))
        {
            zuege.Should().Contain(enthaelt.EnumerateArray().Select(e => e.GetString()!), id);
        }

        if (fall.TryGetProperty("enthaeltNicht", out var enthaeltNicht))
        {
            zuege.Should().NotContain(enthaeltNicht.EnumerateArray().Select(e => e.GetString()!), id);
        }
    }

    [Fact]
    public void Unterverwandlung_per_UCI_setzt_die_gewaehlte_Figur()
    {
        var stellung = Stellung.AusFen("8/P7/8/8/8/8/8/k6K w - - 0 1").Ziehe("a7a8n");

        stellung.Fen.Should().Be("N7/8/8/8/8/8/8/k6K b - - 0 1");
    }

    [Fact]
    public void Unzulaessiger_Zug_wirft_ZugUnzulaessig()
    {
        var stellung = Stellung.AusFen(SchachDomain.GrundstellungFen);

        stellung.Invoking(s => s.Ziehe("e2e5")).Should().Throw<ZugUnzulaessigException>();
        stellung.Invoking(s => s.Ziehe("e7e5")).Should().Throw<ZugUnzulaessigException>();
        stellung.Invoking(s => s.Ziehe("murks")).Should().Throw<ZugUnzulaessigException>();
    }

    [Fact]
    public void Rochade_ist_e1g1_und_setzt_den_Turm()
    {
        var stellung = Stellung.AusFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");

        stellung.ErlaubteZuege().Should().Contain("e1g1").And.NotContain("e1h1");
        stellung.Ziehe("e1g1").Fen.Should().Be("r3k2r/8/8/8/8/8/8/R4RK1 b kq - 1 1");
    }

    [Fact]
    public void Matt_Patt_und_Schach_kommen_aus_Gera_Chess()
    {
        var matt = Stellung.AusFen(SchachDomain.GrundstellungFen).Ziehe("f2f3", "e7e5", "g2g4", "d8h4");
        matt.IstMatt.Should().BeTrue();
        matt.IstSchach.Should().BeTrue();
        matt.IstPatt.Should().BeFalse();
        matt.AmZug.Should().Be(Farbe.Weiss);
        matt.ErlaubteZuege().Should().BeEmpty();

        var patt = Stellung.AusFen("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
        patt.IstPatt.Should().BeTrue();
        patt.IstMatt.Should().BeFalse();
        patt.IstSchach.Should().BeFalse();
        patt.AmZug.Should().Be(Farbe.Schwarz);
    }
}
