using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Schach.Api.Contracts;
using Schach.Api.Tests.Konto;
using Schach.Tests.Shared;
using E = Schach.Domain.Entitaeten;

namespace Schach.Api.Tests.Partien;

/// <summary>Gast- und Spieler-Partie ueber HTTP mit Fake-Engine (SCHACH-SPEC 3.1 bis 3.3, 8, 15.2).</summary>
public sealed class PartieTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<T> LiesAsync<T>(HttpResponseMessage antwort) =>
        (await antwort.Content.ReadFromJsonAsync<T>(ApiSitzung.Json, Ct))!;

    private static async Task<ZustandDto> StartenAsync(ApiSitzung browser, string gegner, FarbeWahl farbe, Bedenkzeit bedenkzeit = Bedenkzeit.OhneUhr)
    {
        using var antwort = await browser.PostAsync("/api/v1/partien", new NeuePartieAnfrage(gegner, farbe, bedenkzeit));
        antwort.StatusCode.Should().Be(HttpStatusCode.Created);
        var zustand = await LiesAsync<ZustandDto>(antwort);
        antwort.Headers.Location!.ToString().Should().EndWith($"/api/v1/partien/{zustand.Id}");
        return zustand;
    }

    private static async Task<ZustandDto> ZiehenAsync(ApiSitzung browser, ZustandDto zustand, string uci)
    {
        using var antwort = await browser.PostAsync($"/api/v1/partien/{zustand.Id}/zuege", new ZugAnfrage(zustand.Halbzug, uci));
        antwort.StatusCode.Should().Be(HttpStatusCode.OK);
        return await LiesAsync<ZustandDto>(antwort);
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Gast_mit_Schwarz_bekommt_den_ersten_Gegnerzug_in_der_201_Antwort() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);

        var zustand = await StartenAsync(gast, "oscar", FarbeWahl.Schwarz);

        zustand.Status.Should().Be(PartieStatus.Offen);
        zustand.SpielerFarbe.Should().Be(Farbe.Schwarz);
        zustand.Halbzug.Should().Be(1);
        zustand.GegnerZug.Should().NotBeNull();
        zustand.GegnerZug!.Seite.Should().Be(Farbe.Weiss);
        zustand.GegnerZug.SichtbarAb.Should().Be(zustand.GegnerZug.GespieltAm);
        zustand.Zugfolge.Should().ContainSingle();
        zustand.AmZug.Should().BeTrue();
        zustand.ErlaubteZuege.Should().HaveCount(20);
        zustand.ZaehltFuerElo.Should().BeFalse();
        zustand.Uhr.Should().BeNull();

        var geladen = (await gast.Http.GetFromJsonAsync<ZustandDto>($"/api/v1/partien/{zustand.Id}", ApiSitzung.Json, Ct))!;
        geladen.Halbzug.Should().Be(1);
        geladen.GegnerZug.Should().BeNull();
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Gast_Partie_bis_Matt_wird_beim_Beenden_geloescht() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        factory.Engine.Vorgabe("e7e5", "d8h4");
        var zustand = await StartenAsync(gast, "finn", FarbeWahl.Weiss);

        zustand = await ZiehenAsync(gast, zustand, "f2f3");
        zustand.GegnerZug!.Uci.Should().Be("e7e5");
        zustand = await ZiehenAsync(gast, zustand, "g2g4");

        zustand.Status.Should().Be(PartieStatus.Beendet);
        zustand.Ergebnis.Should().Be(Ergebnis.SchwarzGewinnt);
        zustand.Grund.Should().Be(Grund.Matt);
        zustand.GegnerZug!.San.Should().Be("Qh4#");
        zustand.Pgn.Should().Contain("2. g4 Qh4# 0-1");
        zustand.EloVorher.Should().BeNull();

        using var weg = await gast.Http.GetAsync(new Uri($"/api/v1/partien/{zustand.Id}", UriKind.Relative), Ct);
        weg.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Zug>().CountAsync(Ct))).Should().Be(0);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Spieler_Partie_mit_Aufgabe_bucht_Elo_und_Bilanz() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        var zustand = await StartenAsync(mia, "oscar", FarbeWahl.Weiss);
        zustand.ZaehltFuerElo.Should().BeTrue();

        using (var zweite = await mia.PostAsync("/api/v1/partien", new NeuePartieAnfrage("theo", FarbeWahl.Zufall, Bedenkzeit.OhneUhr)))
        {
            zweite.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var fehler = await LiesAsync<FehlerAntwort>(zweite);
            fehler.Fehler.Should().Be(FehlerCode.OffenePartie);
            fehler.OffenePartieId.Should().Be(zustand.Id);
        }

        (await mia.Http.GetFromJsonAsync<ZustandDto>("/api/v1/partien/offen", ApiSitzung.Json, Ct))!.Id.Should().Be(zustand.Id);

        zustand = await ZiehenAsync(mia, zustand, "e2e4");
        zustand.GegnerZug.Should().NotBeNull();
        var gespeichert = await AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Zug>().SingleAsync(z => z.PartieId == zustand.Id && z.Halbzug == 2, Ct));
        gespeichert.Engine.Should().Be(E.ZugEngine.Maia);
        gespeichert.MaiaRang.Should().Be(1);
        gespeichert.PAnteil.Should().Be(1.0);
        gespeichert.W.Should().NotBeNull();
        gespeichert.Wdl!.W.Should().Be(0.5);

        using var aufgabe = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/aufgeben", new HalbzugAnfrage(zustand.Halbzug));
        aufgabe.StatusCode.Should().Be(HttpStatusCode.OK);
        zustand = await LiesAsync<ZustandDto>(aufgabe);
        zustand.Status.Should().Be(PartieStatus.Beendet);
        zustand.Ergebnis.Should().Be(Ergebnis.SchwarzGewinnt);
        zustand.Grund.Should().Be(Grund.Aufgabe);
        zustand.EloVorher.Should().Be(800);
        zustand.EloNachher.Should().Be(784); // 800 gegen 800: E = 0,5, 32 * -0,5 = -16.

        using var offen = await mia.Http.GetAsync(new Uri("/api/v1/partien/offen", UriKind.Relative), Ct);
        offen.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var gegner = (await mia.Http.GetFromJsonAsync<List<GegnerDto>>("/api/v1/gegner", ApiSitzung.Json, Ct))!;
        gegner.Single(g => g.Key == "oscar").Bilanz.Should().Be(new BilanzDto(0, 0, 1, null));
        gegner.Single(g => g.Key == "theo").Bilanz.Should().Be(new BilanzDto(0, 0, 0, null));

        using var nochmal = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/aufgeben", new HalbzugAnfrage(zustand.Halbzug));
        nochmal.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LiesAsync<FehlerAntwort>(nochmal)).Fehler.Should().Be(FehlerCode.PartieBeendet);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Sieg_in_zaehlender_Partie_markiert_den_Gegner_als_besiegt() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        factory.Engine.Vorgabe("f7f6", "g7g5");
        var zustand = await StartenAsync(mia, "ilse", FarbeWahl.Weiss);

        zustand = await ZiehenAsync(mia, zustand, "e2e4");
        zustand = await ZiehenAsync(mia, zustand, "d2d4");
        zustand = await ZiehenAsync(mia, zustand, "d1h5");

        zustand.Ergebnis.Should().Be(Ergebnis.WeissGewinnt);
        zustand.Grund.Should().Be(Grund.Matt);
        zustand.GegnerZug.Should().BeNull();
        zustand.EloNachher.Should().BeGreaterThan(800);
        var ilse = (await mia.Http.GetFromJsonAsync<List<GegnerDto>>("/api/v1/gegner", ApiSitzung.Json, Ct))!.Single(g => g.Key == "ilse");
        ilse.Bilanz!.Siege.Should().Be(1);
        ilse.Bilanz.BesiegtAm.Should().NotBeNull();
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Gleicher_Zug_ist_idempotent_anderer_ist_veraltet() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var start = await StartenAsync(gast, "nele", FarbeWahl.Weiss);
        var nachZug = await ZiehenAsync(gast, start, "e2e4");

        var wiederholt = await ZiehenAsync(gast, start, "e2e4");
        wiederholt.Halbzug.Should().Be(nachZug.Halbzug);
        wiederholt.GegnerZug!.Uci.Should().Be(nachZug.GegnerZug!.Uci);
        factory.Engine.Aufrufe.Should().Be(1);

        foreach (var anfrage in new[] { new ZugAnfrage(0, "d2d4"), new ZugAnfrage(7, "d2d4") })
        {
            using var veraltet = await gast.PostAsync($"/api/v1/partien/{start.Id}/zuege", anfrage);
            veraltet.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var fehler = await LiesAsync<FehlerAntwort>(veraltet);
            fehler.Fehler.Should().Be(FehlerCode.HalbzugVeraltet);
            fehler.Zustand!.Halbzug.Should().Be(2);
        }

        using var unzulaessig = await gast.PostAsync($"/api/v1/partien/{start.Id}/zuege", new ZugAnfrage(2, "e2e5"));
        unzulaessig.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await LiesAsync<FehlerAntwort>(unzulaessig)).Fehler.Should().Be(FehlerCode.ZugUnzulaessig);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Gleichzeitige_Zuege_ergeben_einmal_200_und_einmal_409() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var start = await StartenAsync(gast, "nele", FarbeWahl.Weiss);
        await gast.SitzungAsync();

        var antworten = await Task.WhenAll(
            gast.Http.PostAsJsonAsync($"/api/v1/partien/{start.Id}/zuege", new ZugAnfrage(0, "e2e4"), ApiSitzung.Json, Ct),
            gast.Http.PostAsJsonAsync($"/api/v1/partien/{start.Id}/zuege", new ZugAnfrage(0, "d2d4"), ApiSitzung.Json, Ct));

        antworten.Select(a => a.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        foreach (var a in antworten)
        {
            a.Dispose();
        }
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Ohne_Engine_bleibt_der_Gegnerzug_ausstehend() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var start = await StartenAsync(gast, "theo", FarbeWahl.Weiss);
        factory.Engine.Modus = FakeEngineModus.Ausfall;

        var zustand = await ZiehenAsync(gast, start, "e2e4");

        zustand.GegnerZugAusstehend.Should().BeTrue();
        zustand.GegnerZug.Should().BeNull();
        zustand.AmZug.Should().BeFalse();
        using var nichtAmZug = await gast.PostAsync($"/api/v1/partien/{start.Id}/zuege", new ZugAnfrage(1, "d2d4"));
        nichtAmZug.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LiesAsync<FehlerAntwort>(nichtAmZug)).Fehler.Should().Be(FehlerCode.NichtAmZug);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Fairy_Ersatz_spielt_bestmove_und_speichert_die_Engine() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        factory.Engine.Modus = FakeEngineModus.Fairy;
        factory.Engine.Vorgabe("c7c5");
        var start = await StartenAsync(gast, "maren", FarbeWahl.Weiss);

        var zustand = await ZiehenAsync(gast, start, "e2e4");

        zustand.GegnerZug!.Uci.Should().Be("c7c5");
        var zug = await AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Zug>().SingleAsync(z => z.PartieId == start.Id && z.Halbzug == 2, Ct));
        zug.Engine.Should().Be(E.ZugEngine.Fairy);
        zug.MaiaRang.Should().BeNull();
        zug.W.Should().BeNull();
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Zuruecknehmen_entfernt_Zug_und_Antwort_und_zaehlt_nicht_mehr() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        var zustand = await StartenAsync(mia, "konrad", FarbeWahl.Weiss);
        zustand = await ZiehenAsync(mia, zustand, "e2e4");

        using var antwort = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/zuruecknehmen", new HalbzugAnfrage(zustand.Halbzug));

        antwort.StatusCode.Should().Be(HttpStatusCode.OK);
        var danach = await LiesAsync<ZustandDto>(antwort);
        danach.Halbzug.Should().Be(0);
        danach.Zugfolge.Should().BeEmpty();
        danach.ZaehltFuerElo.Should().BeFalse();
        danach.AmZug.Should().BeTrue();
        (await AuthAblaufTests.MitDbAsync(factory, db => db.Set<E.Zug>().CountAsync(z => z.PartieId == zustand.Id, Ct))).Should().Be(0);

        using var ohneEigenenZug = await mia.PostAsync($"/api/v1/partien/{zustand.Id}/zuruecknehmen", new HalbzugAnfrage(0));
        ohneEigenenZug.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LiesAsync<FehlerAntwort>(ohneEigenenZug)).Fehler.Should().Be(FehlerCode.ZuruecknehmenNichtErlaubt);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Mit_Bedenkzeit_laeuft_die_Uhr_und_es_gibt_kein_Zuruecknehmen() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var zustand = await StartenAsync(gast, "sementa", FarbeWahl.Weiss, Bedenkzeit.Fischer5Plus3);
        zustand.Uhr!.LaeuftFuer.Should().Be(UhrSeite.Spieler);
        zustand = await ZiehenAsync(gast, zustand, "e2e4");
        zustand.Uhr!.LaeuftFuer.Should().Be(UhrSeite.Spieler);

        using var antwort = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/zuruecknehmen", new HalbzugAnfrage(zustand.Halbzug));

        antwort.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await LiesAsync<FehlerAntwort>(antwort)).Fehler.Should().Be(FehlerCode.ZuruecknehmenNichtErlaubt);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Spieler_Partie_sehen_andere_nicht() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var mia = new ApiSitzung(factory);
        await mia.AnmeldenAsync("mia@example.test");
        var zustand = await StartenAsync(mia, "oscar", FarbeWahl.Weiss);
        using var gast = new ApiSitzung(factory);

        using var laden = await gast.Http.GetAsync(new Uri($"/api/v1/partien/{zustand.Id}", UriKind.Relative), Ct);
        using var ziehen = await gast.PostAsync($"/api/v1/partien/{zustand.Id}/zuege", new ZugAnfrage(0, "e2e4"));

        laden.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ziehen.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var offen = await gast.Http.GetAsync(new Uri("/api/v1/partien/offen", UriKind.Relative), Ct);
        offen.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Gegnerliste_fuer_Gaeste_ohne_Bilanz_und_Elo_aufsteigend() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);

        var gegner = (await gast.Http.GetFromJsonAsync<List<GegnerDto>>("/api/v1/gegner", ApiSitzung.Json, Ct))!;

        gegner.Select(g => g.Key).Should().Equal("ilse", "finn", "oscar", "nele", "theo", "sementa", "konrad", "maren");
        gegner.Should().OnlyContain(g => g.Bilanz == null);
        gegner[2].Should().Be(new GegnerDto("oscar", "Oscar", 800, Stufe.Anfaenger, Persoenlichkeit.Lehrerhaft,
            "Ich erklär dir hinterher gern, warum ich so gezogen hab.", null));
    });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Unbekannter_Gegner_ist_400() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);

        using var antwort = await gast.PostAsync("/api/v1/partien", new NeuePartieAnfrage("magnus", FarbeWahl.Weiss, Bedenkzeit.OhneUhr));

        antwort.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    });
}
