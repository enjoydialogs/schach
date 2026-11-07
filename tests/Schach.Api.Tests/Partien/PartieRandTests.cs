using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Schach.Api.Contracts;
using Schach.Api.Tests.Konto;
using Schach.Application.Partien;
using Schach.Tests.Shared;
using E = Schach.Domain.Entitaeten;

namespace Schach.Api.Tests.Partien;

/// <summary>Partie-Rand ueber HTTP (SCHACH-SPEC 3.5, 6.1, 7.3, 8.3, 8.5, 8.7): Remis, Tipp, Zeit, Sweep,
/// Sprueche, PGN und Partienliste.</summary>
public sealed class PartieRandTests
{
    /// <summary>Grundstellung mit Zugnummer 25: fuer die Remis-Bewertung zaehlt nur FEN-Feld 6.</summary>
    private const string GrundstellungZug25 = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 25";

    private static readonly string[] ListenGegner = ["ilse", "finn", "oscar"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> LiesAsync<T>(HttpResponseMessage antwort) =>
        (await antwort.Content.ReadFromJsonAsync<T>(ApiSitzung.Json, Ct))!;

    private static async Task<ZustandDto> StartenAsync(ApiSitzung browser, string gegner, FarbeWahl farbe, Bedenkzeit bedenkzeit = Bedenkzeit.OhneUhr)
    {
        using var antwort = await browser.PostAsync("/api/v1/partien", new NeuePartieAnfrage(gegner, farbe, bedenkzeit));
        antwort.StatusCode.Should().Be(HttpStatusCode.Created);
        return await LiesAsync<ZustandDto>(antwort);
    }

    private static async Task<ZustandDto> ZiehenAsync(ApiSitzung browser, ZustandDto zustand, string uci)
    {
        using var antwort = await browser.PostAsync($"/api/v1/partien/{zustand.Id}/zuege", new ZugAnfrage(zustand.Halbzug, uci));
        antwort.StatusCode.Should().Be(HttpStatusCode.OK);
        return await LiesAsync<ZustandDto>(antwort);
    }

    private static async Task<FehlerAntwort> FehlerAsync(HttpResponseMessage antwort, HttpStatusCode status, FehlerCode code)
    {
        antwort.StatusCode.Should().Be(status);
        var fehler = await LiesAsync<FehlerAntwort>(antwort);
        fehler.Fehler.Should().Be(code);
        return fehler;
    }

    private static Task<ZustandDto> LadenAsync(ApiSitzung browser, Guid id) =>
        browser.Http.GetFromJsonAsync<ZustandDto>($"/api/v1/partien/{id}", ApiSitzung.Json, Ct)!;

    private static Task<int> FenSetzenAsync(SchachApiFactory factory, Guid id, string fen) =>
        AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Partie>().Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Fen, fen), Ct));

    private static async Task<T> SweepAsync<T>(SchachApiFactory factory, Func<PartieSweep, Task<T>> block)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await block(scope.ServiceProvider.GetRequiredService<PartieSweep>());
    }

    // --- Sprueche ---

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Start_bringt_den_Partiebeginn_Spruch_und_Aufgabe_den_Sieg_Spruch() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        var zustand = await StartenAsync(mia, "oscar", FarbeWahl.Weiss);

        zustand.Sprueche.Should().ContainSingle().Which.Should().Match<SpruchDto>(s => s.Ereignis == SpruchEreignis.Partiebeginn && s.NachHalbzug == 0);

        using var aufgabe = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/aufgeben", new HalbzugAnfrage(0));
        var ende = await LiesAsync<ZustandDto>(aufgabe);
        ende.Sprueche.Select(s => s.Ereignis).Should().Equal(SpruchEreignis.Partiebeginn, SpruchEreignis.Sieg);
        var gespeichert = await AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Spruch>().Where(s => s.PartieId == zustand.Id).ToListAsync(Ct));
        gespeichert.Should().HaveCount(2).And.OnlyContain(s => s.Quelle == E.SpruchQuelle.Ersatz);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Erste_Zuruecknahme_kommentiert_der_Gegner() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var zustand = await ZiehenAsync(gast, await StartenAsync(gast, "nele", FarbeWahl.Weiss), "e2e4");

        using var antwort = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/zuruecknehmen", new HalbzugAnfrage(zustand.Halbzug));

        var danach = await LiesAsync<ZustandDto>(antwort);
        danach.Sprueche.Select(s => s.Ereignis).Should().Equal(SpruchEreignis.Partiebeginn, SpruchEreignis.Zuruecknahme);
        danach.Sprueche[^1].NachHalbzug.Should().Be(0);
    });

    // --- Remis-Angebot ---

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Remis_vor_Zug_20_wird_abgelehnt_und_sperrt_fuer_5_Zuege() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var zustand = await StartenAsync(gast, "finn", FarbeWahl.Weiss);

        using var angebot = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/remis-angebot", new HalbzugAnfrage(0));

        angebot.StatusCode.Should().Be(HttpStatusCode.OK);
        var abgelehnt = await LiesAsync<ZustandDto>(angebot);
        abgelehnt.Status.Should().Be(PartieStatus.Offen);
        abgelehnt.RemisAngebotMoeglichAbZug.Should().Be(6);
        abgelehnt.Sprueche[^1].Ereignis.Should().Be(SpruchEreignis.RemisAbgelehnt);
        factory.Engine.AnalyseAufrufe.Should().Be(0);

        using var nochmal = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/remis-angebot", new HalbzugAnfrage(0));
        var fehler = await FehlerAsync(nochmal, HttpStatusCode.Conflict, FehlerCode.RemisAngebotGesperrt);
        fehler.Zustand!.RemisAngebotMoeglichAbZug.Should().Be(6);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Remis_ab_Zug_20_nimmt_der_Gegner_bei_ausgeglichener_Stellung_an() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        var zustand = await StartenAsync(mia, "oscar", FarbeWahl.Weiss);
        await FenSetzenAsync(factory, zustand.Id, GrundstellungZug25);
        factory.Engine.AnalyseCp = 40;

        using var angebot = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/remis-angebot", new HalbzugAnfrage(0));

        var ende = await LiesAsync<ZustandDto>(angebot);
        ende.Status.Should().Be(PartieStatus.Beendet);
        ende.Ergebnis.Should().Be(Ergebnis.Remis);
        ende.Grund.Should().Be(Grund.Vereinbarung);
        ende.Sprueche[^1].Ereignis.Should().Be(SpruchEreignis.Remis);
        ende.EloNachher.Should().NotBeNull();
        factory.Engine.AnalyseAufrufe.Should().Be(1);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Remis_lehnt_der_Gegner_ab_wenn_er_klar_vorn_steht() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var zustand = await StartenAsync(gast, "konrad", FarbeWahl.Weiss);
        await FenSetzenAsync(factory, zustand.Id, GrundstellungZug25);
        // Spieler am Zug: -300 fuer ihn sind +300 fuer den Gegner.
        factory.Engine.AnalyseCp = -300;

        using var angebot = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/remis-angebot", new HalbzugAnfrage(0));

        var abgelehnt = await LiesAsync<ZustandDto>(angebot);
        abgelehnt.Status.Should().Be(PartieStatus.Offen);
        abgelehnt.RemisAngebotMoeglichAbZug.Should().Be(30);
        abgelehnt.Sprueche[^1].Ereignis.Should().Be(SpruchEreignis.RemisAbgelehnt);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Remis_bei_Engine_Ausfall_entscheidet_das_Material() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var zustand = await StartenAsync(gast, "theo", FarbeWahl.Weiss);
        await FenSetzenAsync(factory, zustand.Id, GrundstellungZug25);
        factory.Engine.AnalyseCp = null;

        using var angebot = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/remis-angebot", new HalbzugAnfrage(0));

        var ende = await LiesAsync<ZustandDto>(angebot);
        ende.Grund.Should().Be(Grund.Vereinbarung);
        using var weg = await gast.Http.GetAsync(new Uri($"/api/v1/partien/{zustand.Id}", UriKind.Relative), Ct);
        weg.StatusCode.Should().Be(HttpStatusCode.NotFound);
    });

    // --- Tipp ---

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Tipp_liefert_den_besten_Zug_cacht_je_FEN_und_nimmt_die_Partie_aus_der_Elo() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        var zustand = await StartenAsync(mia, "oscar", FarbeWahl.Weiss);
        zustand.ZaehltFuerElo.Should().BeTrue();

        using var erster = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/tipp", new HalbzugAnfrage(0));

        erster.StatusCode.Should().Be(HttpStatusCode.OK);
        var tipp = await LiesAsync<TippAntwort>(erster);
        tipp.Tipp.Uci.Should().Be(zustand.ErlaubteZuege.Order(StringComparer.Ordinal).First());
        tipp.Tipp.Von.Should().Be(tipp.Tipp.Uci[..2]);
        tipp.Tipp.Nach.Should().Be(tipp.Tipp.Uci[2..4]);
        tipp.Tipp.Figur.Should().BeOneOf(Figur.Springer, Figur.Bauer);
        tipp.Zustand.ZaehltFuerElo.Should().BeFalse();
        tipp.Zustand.Halbzug.Should().Be(0);

        using var zweiter = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/tipp", new HalbzugAnfrage(0));
        (await LiesAsync<TippAntwort>(zweiter)).Tipp.Should().Be(tipp.Tipp);
        factory.Engine.AnalyseAufrufe.Should().Be(1);

        using var veraltet = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/tipp", new HalbzugAnfrage(3));
        await FehlerAsync(veraltet, HttpStatusCode.Conflict, FehlerCode.HalbzugVeraltet);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Tipp_mit_Bedenkzeit_ist_nicht_erlaubt() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var zustand = await StartenAsync(gast, "finn", FarbeWahl.Weiss, Bedenkzeit.Fischer10Plus5);

        using var antwort = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/tipp", new HalbzugAnfrage(0));

        var fehler = await FehlerAsync(antwort, HttpStatusCode.Conflict, FehlerCode.TippNichtErlaubt);
        fehler.Zustand!.Id.Should().Be(zustand.Id);
        factory.Engine.AnalyseAufrufe.Should().Be(0);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Tipp_nicht_am_Zug_ist_409_und_ohne_Stockfish_503() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var start = await StartenAsync(gast, "theo", FarbeWahl.Weiss);
        factory.Engine.AnalyseCp = null;

        using (var ohneStockfish = await gast.PostAsync($"/api/v1/partien/{start.Id}/tipp", new HalbzugAnfrage(0)))
        {
            var fehler = await FehlerAsync(ohneStockfish, HttpStatusCode.ServiceUnavailable, FehlerCode.TippNichtVerfuegbar);
            fehler.Zustand.Should().BeNull();
        }

        factory.Engine.Modus = FakeEngineModus.Ausfall;
        var ausstehend = await ZiehenAsync(gast, start, "e2e4");
        ausstehend.GegnerZugAusstehend.Should().BeTrue();

        using var nichtAmZug = await gast.PostAsync($"/api/v1/partien/{start.Id}/tipp", new HalbzugAnfrage(1));
        await FehlerAsync(nichtAmZug, HttpStatusCode.Conflict, FehlerCode.NichtAmZug);
    });

    // --- Zeitablauf und Sweep ---

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Abgelaufene_Uhr_beendet_beim_Laden_und_der_Zug_danach_ist_409_UhrAbgelaufen()
    {
        var zeit = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await AuthAblaufTests.MitApiAsync(
            async factory =>
            {
                using var mia = new ApiSitzung(factory);
                await mia.AnmeldenAsync("mia@example.test");
                var zustand = await StartenAsync(mia, "oscar", FarbeWahl.Weiss, Bedenkzeit.Fischer5Plus3);

                zeit.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
                var geladen = await LadenAsync(mia, zustand.Id);

                geladen.Status.Should().Be(PartieStatus.Beendet);
                geladen.Ergebnis.Should().Be(Ergebnis.SchwarzGewinnt);
                geladen.Grund.Should().Be(Grund.Zeit);
                geladen.Uhr!.LaeuftFuer.Should().BeNull();
                geladen.Sprueche[^1].Ereignis.Should().Be(SpruchEreignis.Sieg);

                using var zuSpaet = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/zuege", new ZugAnfrage(0, "e2e4"));
                await FehlerAsync(zuSpaet, HttpStatusCode.Conflict, FehlerCode.UhrAbgelaufen);
            },
            zeit: zeit);
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Zug_nach_Ablauf_ohne_vorheriges_Laden_ist_409_UhrAbgelaufen()
    {
        var zeit = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await AuthAblaufTests.MitApiAsync(
            async factory =>
            {
                using var gast = new ApiSitzung(factory);
                var zustand = await StartenAsync(gast, "finn", FarbeWahl.Weiss, Bedenkzeit.JeZug90s);

                zeit.Advance(TimeSpan.FromSeconds(91));
                using var zuSpaet = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/zuege", new ZugAnfrage(0, "e2e4"));

                var fehler = await FehlerAsync(zuSpaet, HttpStatusCode.Conflict, FehlerCode.UhrAbgelaufen);
                fehler.Zustand!.Status.Should().Be(PartieStatus.Beendet);
                fehler.Zustand.Grund.Should().Be(Grund.Zeit);
            },
            zeit: zeit);
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Sweep_A_beendet_abgelaufene_Uhren_mit_Ersatzspruch()
    {
        var zeit = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await AuthAblaufTests.MitApiAsync(
            async factory =>
            {
                using var mia = new ApiSitzung(factory);
                await mia.AnmeldenAsync("mia@example.test");
                var zustand = await StartenAsync(mia, "sementa", FarbeWahl.Weiss, Bedenkzeit.Fischer5Plus3);

                (await SweepAsync(factory, s => s.UhrenAsync(Ct))).Should().Be(0);
                zeit.Advance(TimeSpan.FromMinutes(6));
                (await SweepAsync(factory, s => s.UhrenAsync(Ct))).Should().Be(1);

                var partie = await AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Partie>().SingleAsync(p => p.Id == zustand.Id, Ct));
                partie.Status.Should().Be(E.PartieStatus.Beendet);
                partie.Grund.Should().Be(Schach.Domain.Grund.Zeit);
                partie.EloNachher.Should().BeLessThan(800);
                var spruch = await AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Spruch>().Where(s => s.PartieId == zustand.Id).OrderBy(s => s.Id).LastAsync(Ct));
                spruch.Ereignis.Should().Be(E.SpruchEreignis.Sieg);
                spruch.Quelle.Should().Be(E.SpruchQuelle.Ersatz);
            },
            zeit: zeit);
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Sweep_B_holt_den_Gegnerzug_nach_und_belastet_nur_die_Denkzeit()
    {
        var zeit = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await AuthAblaufTests.MitApiAsync(
            async factory =>
            {
                using var gast = new ApiSitzung(factory);
                var start = await StartenAsync(gast, "nele", FarbeWahl.Weiss, Bedenkzeit.Fischer5Plus3);
                factory.Engine.Modus = FakeEngineModus.Ausfall;
                (await ZiehenAsync(gast, start, "e2e4")).GegnerZugAusstehend.Should().BeTrue();

                zeit.Advance(TimeSpan.FromSeconds(5));
                (await SweepAsync(factory, s => s.GegnerzuegeAsync(Ct))).Should().Be(0);

                factory.Engine.Modus = FakeEngineModus.Maia;
                zeit.Advance(TimeSpan.FromSeconds(55));
                (await SweepAsync(factory, s => s.GegnerzuegeAsync(Ct))).Should().Be(1);

                var zustand = await LadenAsync(gast, start.Id);
                zustand.Halbzug.Should().Be(2);
                zustand.AmZug.Should().BeTrue();
                // 5 min Start, hoechstens 3 s Denkzeit ab, 3 s Inkrement dazu: die Ausfallminute zaehlt nicht.
                zustand.Uhr!.GegnerRestMs.Should().BeGreaterThanOrEqualTo(300_000);
                (await SweepAsync(factory, s => s.GegnerzuegeAsync(Ct))).Should().Be(0);
            },
            zeit: zeit);
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Sweep_C_loescht_Gast_Partien_ohne_Zug_seit_24_Stunden()
    {
        var zeit = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await AuthAblaufTests.MitApiAsync(
            async factory =>
            {
                using var gast = new ApiSitzung(factory);
                using var mia = new ApiSitzung(factory);
                await mia.AnmeldenAsync("mia@example.test");
                var gastPartie = await StartenAsync(gast, "ilse", FarbeWahl.Weiss);
                var spielerPartie = await StartenAsync(mia, "ilse", FarbeWahl.Weiss);

                zeit.Advance(TimeSpan.FromHours(23));
                (await SweepAsync(factory, s => s.GastPartienAsync(Ct))).Should().Be(0);
                zeit.Advance(TimeSpan.FromHours(2));
                (await SweepAsync(factory, s => s.GastPartienAsync(Ct))).Should().Be(1);

                using var weg = await gast.Http.GetAsync(new Uri($"/api/v1/partien/{gastPartie.Id}", UriKind.Relative), Ct);
                weg.StatusCode.Should().Be(HttpStatusCode.NotFound);
                (await LadenAsync(mia, spielerPartie.Id)).Status.Should().Be(PartieStatus.Offen);
            },
            zeit: zeit);
    }

    // --- PGN und Partienliste ---

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Pgn_nur_fuer_beendete_Partien() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        var zustand = await ZiehenAsync(mia, await StartenAsync(mia, "oscar", FarbeWahl.Weiss), "e2e4");

        using (var offen = await mia.Http.GetAsync(new Uri($"/api/v1/partien/{zustand.Id}/pgn", UriKind.Relative), Ct))
        {
            offen.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        using (var aufgabe = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/aufgeben", new HalbzugAnfrage(zustand.Halbzug)))
        {
            aufgabe.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var pgn = await mia.Http.GetAsync(new Uri($"/api/v1/partien/{zustand.Id}/pgn", UriKind.Relative), Ct);
        pgn.StatusCode.Should().Be(HttpStatusCode.OK);
        pgn.Content.Headers.ContentType!.MediaType.Should().Be("application/x-chess-pgn");
        var text = await pgn.Content.ReadAsStringAsync(Ct);
        text.Should().Contain("1. e4").And.Contain("0-1");

        using var fremd = new ApiSitzung(factory);
        using var nichtSeine = await fremd.Http.GetAsync(new Uri($"/api/v1/partien/{zustand.Id}/pgn", UriKind.Relative), Ct);
        nichtSeine.StatusCode.Should().Be(HttpStatusCode.NotFound);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Partienliste_blaettert_per_Keyset_neueste_zuerst()
    {
        // Die Uhr steht: alle Partien enden zur selben Zeit, die Reihenfolge entscheidet die Id.
        var zeit = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await AuthAblaufTests.MitApiAsync(
            async factory =>
            {
                using var mia = new ApiSitzung(factory);
                await mia.AnmeldenAsync("mia@example.test");
                var ids = new List<Guid>();
                foreach (var gegner in ListenGegner)
                {
                    var zustand = await StartenAsync(mia, gegner, FarbeWahl.Weiss);
                    using var aufgabe = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/aufgeben", new HalbzugAnfrage(0));
                    aufgabe.StatusCode.Should().Be(HttpStatusCode.OK);
                    ids.Add(zustand.Id);
                }

                var offen = await StartenAsync(mia, "nele", FarbeWahl.Weiss);

                var erste = (await mia.Http.GetFromJsonAsync<PartienListeDto>("/api/v1/partien?limit=2", ApiSitzung.Json, Ct))!;
                erste.Partien.Should().HaveCount(2);
                erste.Weitere.Should().BeTrue();
                var zweite = (await mia.Http.GetFromJsonAsync<PartienListeDto>($"/api/v1/partien?limit=2&vorId={erste.Partien[^1].Id}", ApiSitzung.Json, Ct))!;
                zweite.Partien.Should().ContainSingle();
                zweite.Weitere.Should().BeFalse();

                var alle = erste.Partien.Concat(zweite.Partien).ToList();
                alle.Select(p => p.Id).Should().BeEquivalentTo(ids).And.NotContain(offen.Id);
                alle[0].Ergebnis.Should().Be(Ergebnis.SchwarzGewinnt);
                alle[0].Grund.Should().Be(Grund.Aufgabe);
                alle[0].ZaehltFuerElo.Should().BeTrue();
                alle.Should().OnlyContain(p => p.EloNachher != null);

                var ganz = (await mia.Http.GetFromJsonAsync<PartienListeDto>("/api/v1/partien", ApiSitzung.Json, Ct))!;
                ganz.Partien.Select(p => p.Id).Should().Equal(alle.Select(p => p.Id));

                using var unbekannt = await mia.Http.GetAsync(new Uri($"/api/v1/partien?vorId={Guid.NewGuid()}", UriKind.Relative), Ct);
                unbekannt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                using var gast = new ApiSitzung(factory);
                using var ohneKonto = await gast.Http.GetAsync(new Uri("/api/v1/partien", UriKind.Relative), Ct);
                ohneKonto.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            },
            zeit: zeit);
    }
}
