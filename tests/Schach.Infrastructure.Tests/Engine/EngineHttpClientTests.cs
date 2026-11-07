using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Engine;
using Schach.Infrastructure.Telemetry;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests.Engine;

/// <summary>Verhalten von <see cref="EngineHttpClient"/> hinter <see cref="IEngineClient"/>
/// (SCHACH-SPEC 5.2, 8.10) gegen einen nachgespielten Engine-Dienst.</summary>
public sealed class EngineHttpClientTests
{
    private const string Grundstellung = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    private const string MaiaAntwort = """
        {"engine":"maia","policy":[{"uci":"e7e5","p":0.6},{"uci":"c7c5","p":0.4}],"wdl":{"w":0.41,"d":0.18,"l":0.41},"bestmove":null}
        """;

    private static readonly Messwerte OhneAuswertung = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static EngineMoveRequest MaiaAnfrage => new(Grundstellung, ["e2e4"], 1000, 800, EngineArt.Maia);

    private static EngineHttpClient Client(FakeEngineHandler handler, Messwerte? messwerte = null, EngineUeberlastWaechter? waechter = null, TimeSpan? gesamt = null)
    {
        var options = new EngineClientOptions
        {
            BaseUrl = new Uri("http://engine:8081"),
            GesamtTimeout = gesamt ?? TimeSpan.FromSeconds(5),
            MaiaTimeout = TimeSpan.FromMilliseconds(200),
        };
        var http = new HttpClient(handler) { BaseAddress = options.BaseUrl, Timeout = options.GesamtTimeout };
        var factory = (messwerte ?? OhneAuswertung).Factory;
        return new EngineHttpClient(http, Options.Create(options), new SchachZaehler(factory), waechter ?? NeuerWaechter(1000));
    }

    private static EngineUeberlastWaechter NeuerWaechter(int fehler) => new(
        Options.Create(new EngineUeberlastOptions { UeberlastFehlerIn30s = fehler }),
        TimeProvider.System,
        NullLogger<EngineUeberlastWaechter>.Instance);

    [Fact]
    public async Task Ausfaelle_zaehlen_im_Meter_mit_art_und_im_Ueberlastwaechter()
    {
        using var messwerte = new Messwerte();
        var waechter = NeuerWaechter(3);
        var handler = new FakeEngineHandler()
            .Json(HttpStatusCode.ServiceUnavailable, """{"error":"queue_full","detail":"x"}""")
            .Verbindungsfehler()
            .Json(HttpStatusCode.OK, """{"engine":"fairy","policy":null,"wdl":null,"bestmove":"e7e5"}""");

        await Client(handler, messwerte, waechter).MoveAsync(MaiaAnfrage, Ct);

        messwerte.Von(SchachMetrics.EngineFehler).Select(t => t["art"])
            .Should().BeEquivalentTo(["Ueberlastet", "Verbindung", "FairyErsatz"]);
        messwerte.Von(SchachMetrics.EngineFehler).Should().AllSatisfy(t => t.Keys.Should().Equal("art"));
        waechter.IstGesperrt.Should().BeFalse("zwei Ausfaelle liegen unter der Schwelle 3");

        await Client(new FakeEngineHandler().Verbindungsfehler(), messwerte, waechter).IsHealthyAsync(Ct);
        await Client(new FakeEngineHandler().Json(HttpStatusCode.ServiceUnavailable, "{}"), messwerte, waechter)
            .Invoking(c => c.AnalyseAsync(new EngineAnalyseRequest(Grundstellung, [], 800, 1), Ct))
            .Should().ThrowAsync<EngineNichtVerfuegbarException>();
        waechter.IstGesperrt.Should().BeTrue();
    }

    [Fact]
    public async Task Vertragsfehler_sperren_nicht()
    {
        var waechter = NeuerWaechter(1);
        var handler = new FakeEngineHandler().Json(HttpStatusCode.BadRequest, """{"error":"invalid_fen","detail":"x"}""");

        await Client(handler, waechter: waechter).Invoking(c => c.MoveAsync(MaiaAnfrage, Ct)).Should().ThrowAsync<EngineFehlerException>();

        waechter.IstGesperrt.Should().BeFalse();
    }

    [Fact]
    public async Task Maia_Zug_schickt_snake_case_an_move_und_liefert_die_Policy()
    {
        var handler = new FakeEngineHandler().Json(HttpStatusCode.OK, MaiaAntwort);

        var antwort = await Client(handler).MoveAsync(MaiaAnfrage, Ct);

        antwort.Engine.Should().Be(EngineArt.Maia);
        antwort.Policy!.Select(p => p.Uci).Should().Equal("e7e5", "c7c5");
        var anfrage = handler.Anfragen.Should().ContainSingle().Subject;
        anfrage.Methode.Should().Be(HttpMethod.Post);
        anfrage.Pfad.Should().Be("/move");
        anfrage.Body.Should().Contain("\"self_elo\":1000").And.Contain("\"engine\":\"maia\"");
    }

    [Fact]
    public async Task Maia_ueber_der_Frist_wird_einmal_wiederholt()
    {
        var handler = new FakeEngineHandler().Haengt().Json(HttpStatusCode.OK, MaiaAntwort);

        var antwort = await Client(handler).MoveAsync(MaiaAnfrage, Ct);

        antwort.Engine.Should().Be(EngineArt.Maia);
        handler.Anfragen.Should().HaveCount(2);
    }

    [Fact]
    public async Task Zweimal_Maia_503_faellt_auf_Fairy_Stockfish_zurueck()
    {
        var handler = new FakeEngineHandler()
            .Json(HttpStatusCode.ServiceUnavailable, """{"error":"queue_full","detail":"x"}""")
            .Json(HttpStatusCode.ServiceUnavailable, """{"error":"queue_full","detail":"x"}""")
            .Json(HttpStatusCode.OK, """{"engine":"fairy","policy":null,"wdl":null,"bestmove":"e7e5"}""");

        var antwort = await Client(handler).MoveAsync(MaiaAnfrage, Ct);

        antwort.Engine.Should().Be(EngineArt.Fairy);
        antwort.Bestmove.Should().Be("e7e5");
        handler.Anfragen.Should().HaveCount(3);
        handler.Anfragen[2].Body.Should().Contain("\"engine\":\"fairy\"").And.Contain("\"fairy_skill\":1000");
    }

    [Fact]
    public async Task Fairy_Ersatz_nimmt_mindestens_UCI_Elo_500()
    {
        var handler = new FakeEngineHandler()
            .Verbindungsfehler()
            .Verbindungsfehler()
            .Json(HttpStatusCode.OK, """{"engine":"fairy","policy":null,"wdl":null,"bestmove":"e7e5"}""");

        await Client(handler).MoveAsync(MaiaAnfrage with { SelfElo = 300 }, Ct);

        handler.Anfragen[2].Body.Should().Contain("\"fairy_skill\":500");
    }

    [Fact]
    public async Task Faellt_auch_Fairy_aus_ist_die_Engine_nicht_verfuegbar()
    {
        var handler = new FakeEngineHandler().Json(HttpStatusCode.ServiceUnavailable, """{"error":"starting","detail":"x"}""");

        var act = () => Client(handler).MoveAsync(MaiaAnfrage, Ct);

        await act.Should().ThrowAsync<EngineNichtVerfuegbarException>();
        handler.Anfragen.Should().HaveCount(3);
    }

    [Fact]
    public async Task Programmfehler_400_wird_weder_wiederholt_noch_ersetzt()
    {
        var handler = new FakeEngineHandler().Json(HttpStatusCode.BadRequest, """{"error":"illegal_move","detail":"move 1 is illegal"}""");

        var act = () => Client(handler).MoveAsync(MaiaAnfrage, Ct);

        (await act.Should().ThrowAsync<EngineFehlerException>()).WithMessage("*illegal_move*");
        handler.Anfragen.Should().ContainSingle();
    }

    [Fact]
    public async Task Unlesbare_Antwort_ist_ein_Vertragsfehler()
    {
        var handler = new FakeEngineHandler().Json(HttpStatusCode.OK, "{\"engine\":\"maia\"");

        var act = () => Client(handler).MoveAsync(MaiaAnfrage, Ct);

        await act.Should().ThrowAsync<EngineFehlerException>();
    }

    [Fact]
    public async Task Fairy_auf_Wunsch_ist_ein_einzelner_Versuch()
    {
        var handler = new FakeEngineHandler().Json(HttpStatusCode.ServiceUnavailable, """{"error":"role_disabled","detail":"x"}""");

        var act = () => Client(handler).MoveAsync(MaiaAnfrage with { Engine = EngineArt.Fairy, FairySkill = 600 }, Ct);

        await act.Should().ThrowAsync<EngineNichtVerfuegbarException>();
        handler.Anfragen.Should().ContainSingle();
    }

    [Fact]
    public async Task Analyse_503_ist_nicht_verfuegbar_ohne_Retry()
    {
        var handler = new FakeEngineHandler().Json(HttpStatusCode.ServiceUnavailable, """{"error":"pool_busy","detail":"x"}""");

        var act = () => Client(handler).AnalyseAsync(new EngineAnalyseRequest(Grundstellung, [], 800, 1), Ct);

        await act.Should().ThrowAsync<EngineNichtVerfuegbarException>();
        handler.Anfragen.Should().ContainSingle().Which.Pfad.Should().Be("/analyse");
    }

    [Fact]
    public async Task Analyse_liefert_Zeilen()
    {
        var handler = new FakeEngineHandler().Json(
            HttpStatusCode.OK,
            """{"bestmove":"f1b5","lines":[{"uci":"f1b5","cp":38,"mate":null,"wdl":{"w":0.21,"d":0.74,"l":0.05}}]}""");

        var antwort = await Client(handler).AnalyseAsync(new EngineAnalyseRequest(Grundstellung, [], 800, 1), Ct);

        antwort.Bestmove.Should().Be("f1b5");
        antwort.Lines.Should().ContainSingle().Which.Cp.Should().Be(38);
        handler.Anfragen[0].Body.Should().Contain("\"movetime_ms\":800");
    }

    [Fact]
    public async Task Aeusserer_Timeout_auf_analyse_ist_nicht_verfuegbar()
    {
        var handler = new FakeEngineHandler().Haengt();

        var act = () => Client(handler, gesamt: TimeSpan.FromMilliseconds(200)).AnalyseAsync(new EngineAnalyseRequest(Grundstellung, [], 800, 1), Ct);

        await act.Should().ThrowAsync<EngineNichtVerfuegbarException>();
    }

    [Fact]
    public async Task Abbruch_durch_den_Aufrufer_bleibt_ein_Abbruch()
    {
        var handler = new FakeEngineHandler().Haengt();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var act = () => Client(handler).AnalyseAsync(new EngineAnalyseRequest(Grundstellung, [], 800, 1), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"status":"ok"}""", true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"status":"starting"}""", false)]
    public async Task Healthz_meldet_bereit_nur_bei_200(HttpStatusCode status, string body, bool erwartet)
    {
        var handler = new FakeEngineHandler().Json(status, body);

        (await Client(handler).IsHealthyAsync(Ct)).Should().Be(erwartet);
        handler.Anfragen.Should().ContainSingle().Which.Pfad.Should().Be("/healthz");
    }

    [Fact]
    public async Task Healthz_ohne_Verbindung_ist_nicht_bereit()
    {
        var handler = new FakeEngineHandler().Verbindungsfehler();

        (await Client(handler).IsHealthyAsync(Ct)).Should().BeFalse();
    }
}
