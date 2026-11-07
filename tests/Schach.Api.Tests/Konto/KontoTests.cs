using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Schach.Api.Contracts;
using Schach.Infrastructure.Identity;
using Schach.Tests.Shared;
using E = Schach.Domain.Entitaeten;

namespace Schach.Api.Tests.Konto;

/// <summary>Konto loeschen und Export (SCHACH-SPEC 9.3, 9.7, 12.4).</summary>
public sealed class KontoTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> PartieAnlegenAsync(SchachApiFactory factory, Guid spielerId)
    {
        var partieId = Guid.NewGuid();
        await AuthAblaufTests.MitDbAsync(factory, async db =>
        {
            var start = new DateTimeOffset(2026, 11, 7, 10, 0, 0, TimeSpan.Zero);
            db.Add(new E.Partie
            {
                Id = partieId,
                SpielerId = spielerId,
                GegnerId = "oscar",
                SpielerFarbe = Schach.Domain.Farbe.Weiss,
                Bedenkzeit = E.Bedenkzeit.OhneUhr,
                Status = E.PartieStatus.Beendet,
                Ergebnis = Schach.Domain.Ergebnis.WeissGewinnt,
                Grund = Schach.Domain.Grund.Aufgabe,
                ErstelltAm = start,
                LetzterZugAm = start.AddMinutes(1),
                BeendetAm = start.AddMinutes(2),
                Halbzuege = 2,
                Fen = "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2",
                ZaehltFuerElo = true,
                EloVorher = 800,
                EloNachher = 812,
                Seed = 42,
            });
            await db.SaveChangesAsync(Ct);
            db.Add(new E.Zug { PartieId = partieId, Halbzug = 1, Seite = Schach.Domain.Farbe.Weiss, Uci = "e2e4", San = "e4", FenDanach = "x", GespieltAm = start });
            db.Add(new E.Zug { PartieId = partieId, Halbzug = 2, Seite = Schach.Domain.Farbe.Schwarz, Uci = "e7e5", San = "e5", FenDanach = "y", GespieltAm = start, Engine = E.ZugEngine.Maia });
            db.Add(new E.Spruch { PartieId = partieId, NachHalbzug = 0, Ereignis = E.SpruchEreignis.Partiebeginn, Text = "Na dann!", Quelle = E.SpruchQuelle.Ersatz });
            db.Add(new E.Bilanz { SpielerId = spielerId, GegnerId = "oscar", Siege = 1, BesiegtAm = start.AddMinutes(2) });
            await db.SaveChangesAsync(Ct);
            return 0;
        });
        return partieId;
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Export_liefert_Konto_Elo_Bilanz_und_Partien_mit_PGN_und_Spruechen() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);
        var konto = await browser.AnmeldenAsync("mia@example.test");
        var partieId = await PartieAnlegenAsync(factory, konto.Spieler.Id);
        await browser.SitzungAsync();

        var export = (await browser.Http.GetFromJsonAsync<KontoExportDto>("/api/v1/konto/export", ApiSitzung.Json, Ct))!;

        export.Konto.Id.Should().Be(konto.Spieler.Id);
        export.Konto.Email.Should().Be("mia@example.test");
        export.Elo.Should().Be(800);
        export.Bilanz.Should().ContainSingle().Which.Should().BeEquivalentTo(new { GegnerKey = "oscar", Siege = 1, Remis = 0, Niederlagen = 0 });
        var partie = export.Partien.Should().ContainSingle().Subject;
        partie.Id.Should().Be(partieId);
        partie.Ergebnis.Should().Be(Ergebnis.WeissGewinnt);
        partie.Grund.Should().Be(Grund.Aufgabe);
        partie.Pgn.Should().Contain("[Result \"1-0\"]").And.Contain("1. e4 e5 1-0");
        partie.Sprueche.Should().ContainSingle().Which.Should().Be(new SpruchDto(0, SpruchEreignis.Partiebeginn, "Na dann!"));
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Konto_loeschen_hinterlaesst_null_Zeilen_zur_Konto_Id_und_meldet_ab() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var browser = new ApiSitzung(factory);
        using var zweitgeraet = new ApiSitzung(factory);
        var konto = await browser.AnmeldenAsync("nina@example.test");
        await zweitgeraet.AnmeldenAsync("nina@example.test");
        var id = konto.Spieler.Id;
        var partieId = await PartieAnlegenAsync(factory, id);
        await browser.AnfordernAsync("nina+tag@example.test"); // offene Anmeldung und Limit-Zaehler bleiben sonst liegen
        await browser.AnfordernAsync("nina@example.test");

        using var antwort = await browser.DeleteAsync("/api/v1/konto");

        antwort.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var hash = EmailAdresse.ZaehlHash("nina@example.test");
        var zeilen = await AuthAblaufTests.MitDbAsync(factory, async db => new[]
        {
            await db.Set<E.Spieler>().CountAsync(s => s.Id == id, Ct),
            await db.Set<E.Partie>().CountAsync(p => p.SpielerId == id, Ct),
            await db.Set<E.Zug>().CountAsync(z => z.PartieId == partieId, Ct),
            await db.Set<E.Spruch>().CountAsync(s => s.PartieId == partieId, Ct),
            await db.Set<E.Bilanz>().CountAsync(b => b.SpielerId == id, Ct),
            await db.Set<E.Anmeldung>().CountAsync(a => a.Email == "nina@example.test", Ct),
            await db.Set<E.AnmeldungVersuch>().CountAsync(v => v.EmailHash == hash, Ct),
        });
        zeilen.Should().AllBeEquivalentTo(0);

        (await browser.SitzungAsync()).Angemeldet.Should().BeFalse();
        (await zweitgeraet.SitzungAsync()).Angemeldet.Should().BeFalse();
    });
}
