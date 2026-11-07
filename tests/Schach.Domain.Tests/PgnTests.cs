using Chess;

namespace Schach.Domain.Tests;

/// <summary>PGN-Erzeugung nach SCHACH-SPEC 3.6: der Grund steht im Tag <c>Termination</c>.</summary>
public sealed class PgnTests
{
    private static readonly PgnKopf Kopf = new("Oscar", "Gast", new DateOnly(2026, 11, 7));

    [Fact]
    public void Schaefermatt_ergibt_vollstaendiges_PGN_mit_Termination()
    {
        var pgn = Pgn.Erzeuge(
            Kopf,
            ["e2e4", "e7e5", "f1c4", "b8c6", "d1h5", "g8f6", "h5f7"],
            new Partieende(Ergebnis.WeissGewinnt, Grund.Matt));

        pgn.Should().Be(
            """
            [Event "Schach"]
            [Site "schach.enjoy-dialogs.de"]
            [Date "2026.11.07"]
            [Round "-"]
            [White "Oscar"]
            [Black "Gast"]
            [Result "1-0"]
            [Termination "Matt"]

            1. e4 e5 2. Bc4 Nc6 3. Qh5 Nf6 4. Qxf7# 1-0

            """.ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData(Ergebnis.SchwarzGewinnt, Grund.Aufgabe, "0-1", "Aufgabe")]
    [InlineData(Ergebnis.Remis, Grund.Vereinbarung, "1/2-1/2", "Vereinbarung")]
    [InlineData(Ergebnis.Remis, Grund.ZeitOhneMattmaterial, "1/2-1/2", "ZeitOhneMattmaterial")]
    public void Ergebnis_und_Grund_stehen_in_Result_Termination_und_am_Ende(Ergebnis ergebnis, Grund grund, string result, string termination)
    {
        var pgn = Pgn.Erzeuge(Kopf, ["e2e4"], new Partieende(ergebnis, grund));

        pgn.Should().Contain($"[Result \"{result}\"]\n")
            .And.Contain($"[Termination \"{termination}\"]\n")
            .And.EndWith($"1. e4 {result}\n");
    }

    [Fact]
    public void Offene_Partie_hat_Stern_und_keine_Termination()
    {
        var pgn = Pgn.Erzeuge(Kopf, ["e2e4", "c7c5"], ende: null);

        pgn.Should().Contain("[Result \"*\"]").And.NotContain("Termination").And.EndWith("1. e4 c5 *\n");
    }

    [Fact]
    public void Abweichende_Startstellung_setzt_SetUp_und_FEN_und_beginnt_mit_Schwarz()
    {
        const string fen = "r3k2r/8/8/8/8/8/1p6/R3K2R b KQkq - 0 30";
        var pgn = Pgn.Erzeuge(Kopf with { StartFen = fen }, ["b2a1q", "e1f2", "e8c8"], ende: null);

        pgn.Should().Contain("[SetUp \"1\"]\n[FEN \"" + fen + "\"]\n")
            .And.EndWith("30... bxa1=Q+ 31. Kf2 O-O-O *\n");
    }

    [Fact]
    public void Erzeugtes_PGN_laesst_sich_wieder_laden()
    {
        var pgn = Pgn.Erzeuge(
            Kopf,
            ["e2e4", "d7d5", "e4d5", "d8d5", "b1c3", "d5a5", "g1f3", "c8g4", "f1e2", "b8c6", "e1g1", "e8c8"],
            new Partieende(Ergebnis.Remis, Grund.Vereinbarung));

        ChessBoard.TryLoadFromPgn(pgn, out var brett).Should().BeTrue(pgn);
        brett!.ToFen().Should().StartWith("2kr1bnr/ppp1pppp/2n5/q7/6b1/2N2N2/PPPPBPPP/R1BQ1RK1 w");
    }
}
