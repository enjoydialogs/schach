using Schach.Api.Controllers;
using Schach.Infrastructure.Identity;
using E = Schach.Domain.Entitaeten;

namespace Schach.Api.Tests.Konto;

/// <summary>PGN im Konto-Export (SCHACH-SPEC 9.3) aus der gespeicherten Zugfolge.</summary>
public sealed class ExportPgnTests
{
    [Fact]
    public void Beendete_Partie_hat_Kopf_Zuege_und_Ergebnis()
    {
        var partie = new E.Partie
        {
            GegnerId = "oscar",
            SpielerFarbe = Schach.Domain.Farbe.Schwarz,
            Status = E.PartieStatus.Beendet,
            Ergebnis = Schach.Domain.Ergebnis.SchwarzGewinnt,
            Grund = Schach.Domain.Grund.Aufgabe,
            ErstelltAm = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero),
        };
        E.Zug[] zuege =
        [
            new() { Halbzug = 1, Uci = "e2e4" },
            new() { Halbzug = 2, Uci = "e7e5" },
            new() { Halbzug = 3, Uci = "g1f3" },
        ];

        var pgn = KontoController.ExportPgn(new ExportPartie(partie, zuege, []));

        pgn.Should().Contain("[Date \"2026.11.07\"]")
            .And.Contain("[Black \"Spieler\"]")
            .And.Contain("[Result \"0-1\"]")
            .And.Contain("[Termination \"Aufgabe\"]")
            .And.EndWith("1. e4 e5 2. Nf3 0-1\n");
    }
}
