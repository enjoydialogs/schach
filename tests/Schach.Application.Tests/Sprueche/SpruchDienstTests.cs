using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Schach.Application.Abstractions;
using Schach.Application.Llm;
using Schach.Application.Sprachausgabe;
using Schach.Application.Sprueche;
using Schach.Application.Tests.Llm;
using Schach.Domain.Entitaeten;

namespace Schach.Application.Tests.Sprueche;

/// <summary>
/// Ersatzspruch-Matrix (SCHACH-SPEC 6.2 bis 6.6, 11.1): wann Mistral gefragt wird, und dass jeder
/// Fehler still zum Ersatzspruch fuehrt.
/// </summary>
public sealed class SpruchDienstTests
{
    private static readonly string[] FinnsZugErsatz = ["Weiter geht's.", "Mein Plan läuft.", "Hm, spannend.", "Ich hab da was vor."];

    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeProvider _provider = new();
    private readonly FakeResolver _resolver = new();
    private readonly FakeGuard _guard = new();
    private readonly FakeVerbrauchSpeicher _speicher = new();
    private readonly SpruchOptions _optionen = new();

    public SpruchDienstTests()
    {
        _resolver.Current = _provider;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SpruchDienst Dienst() => new(
        _resolver, _guard, _speicher,
        new ErsatzKatalog(new Dictionary<string, IReadOnlyDictionary<SpruchEreignis, IReadOnlyList<string>>>
        {
            ["finn"] = ErsatzDatei.Parse(ErsatzKatalogTests.Gueltig, "finn.de.md"),
        }),
        new FestePersona(), _optionen, _zeit, NullLogger<SpruchDienst>.Instance);

    private static Partie Partie(bool gast = false, Bedenkzeit bedenkzeit = Bedenkzeit.OhneUhr, long? gegnerRestMs = null, int mistralAufrufe = 0) => new()
    {
        Id = Guid.NewGuid(),
        SpielerId = gast ? null : Guid.NewGuid(),
        GegnerId = "finn",
        Bedenkzeit = bedenkzeit,
        GegnerRestMs = gegnerRestMs,
        MistralAufrufe = mistralAufrufe,
        Seed = 42,
    };

    private static SpruchAnfrage Anfrage(Partie partie, SpruchEreignis ereignis = SpruchEreignis.Zug) => new(
        partie, "Finn", ereignis, 12,
        new SpruchDaten(ereignis.ToString(), 6, "Eröffnung", "Weiss", partie.Bedenkzeit.ToString(), "e4", "e5", false, 0, 0, null, [], null, []),
        new ErsatzWerte(null, 6), []);

    private Task<Spruch> Spruch(Partie partie, SpruchEreignis ereignis = SpruchEreignis.Zug) =>
        Dienst().ErzeugeAsync(Anfrage(partie, ereignis), Ct);

    [Theory]
    [InlineData(LlmBudgetLevel.Ok, false, SpruchQuelle.Mistral)]
    [InlineData(LlmBudgetLevel.Ok, true, SpruchQuelle.Mistral)]
    [InlineData(LlmBudgetLevel.Warn, false, SpruchQuelle.Mistral)]
    [InlineData(LlmBudgetLevel.Warn, true, SpruchQuelle.Ersatz)]
    [InlineData(LlmBudgetLevel.Exhausted, false, SpruchQuelle.Ersatz)]
    [InlineData(LlmBudgetLevel.Exhausted, true, SpruchQuelle.Ersatz)]
    public async Task Budgetleiter_je_Spieler_und_Gast(LlmBudgetLevel stufe, bool gast, SpruchQuelle erwartet)
    {
        _guard.Level = stufe;
        var partie = Partie(gast);

        var spruch = await Spruch(partie);

        spruch.Quelle.Should().Be(erwartet);
        partie.MistralAufrufe.Should().Be(erwartet == SpruchQuelle.Mistral ? 1 : 0);
        _provider.Aufrufe.Should().Be(erwartet == SpruchQuelle.Mistral ? 1 : 0);
    }

    [Fact]
    public async Task Live_Spruch_wird_geprueft_gespeichert_und_gebucht()
    {
        _provider.Antwort = "\"Finn: Attacke, mein Springer!\"";
        var partie = Partie();

        var spruch = await Spruch(partie);

        spruch.Should().BeEquivalentTo(new Spruch
        {
            PartieId = partie.Id,
            NachHalbzug = 12,
            Ereignis = SpruchEreignis.Zug,
            Text = "Attacke, mein Springer!",
            Quelle = SpruchQuelle.Mistral,
        });
        _speicher.Buchungen.Should().Equal((new DateOnly(2026, 11, 1), new LlmBuchung(400, 20, 1, 0, 0)));
        _provider.LetzteAnfrage!.PromptFile.Should().Be("spruch.de.md");
        _provider.LetzteAnfrage.MaxTokens.Should().Be(80);
        _provider.LetzteAnfrage.Temperature.Should().Be(0.8);
        _provider.LetzteAnfrage.Variables.Should().ContainKeys("name", "persona", "daten");
        _provider.LetzteAnfrage.Variables["persona"].Should().Be("Persona finn");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Http_Fehler_fuehren_zum_Ersatzspruch(HttpStatusCode status)
    {
        _provider.Fehler = new HttpRequestException("x", null, status);
        var partie = Partie();

        var spruch = await Spruch(partie);

        spruch.Quelle.Should().Be(SpruchQuelle.Ersatz);
        spruch.Text.Should().BeOneOf(FinnsZugErsatz);
        partie.MistralAufrufe.Should().Be(1);
        _speicher.Buchungen.Should().Equal((new DateOnly(2026, 11, 1), new LlmBuchung(0, 0, 1, 0, 1)));
        _guard.Gemeldet429.Should().Be(status == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Antwort_ohne_Text_fuehrt_zum_Ersatzspruch()
    {
        _provider.Fehler = new InvalidOperationException("kein Textteil");

        (await Spruch(Partie())).Quelle.Should().Be(SpruchQuelle.Ersatz);
    }

    [Fact]
    public async Task Abgelehnte_Antwort_fuehrt_zum_Ersatzspruch_und_wird_trotzdem_gebucht()
    {
        _provider.Antwort = "Du solltest rochieren.";

        var spruch = await Spruch(Partie());

        spruch.Quelle.Should().Be(SpruchQuelle.Ersatz);
        _speicher.Buchungen.Should().ContainSingle().Which.Buchung.Should().Be(new LlmBuchung(400, 20, 1, 0, 0));
    }

    [Fact]
    public async Task Zeitlimit_2500_ms_fuehrt_zum_Ersatzspruch_und_bucht_die_Schaetzung()
    {
        _provider.Haengt = true;
        var partie = Partie();

        var aufgabe = Spruch(partie);
        await _provider.Gestartet.Task.WaitAsync(Ct);
        _zeit.Advance(TimeSpan.FromMilliseconds(2499));
        aufgabe.IsCompleted.Should().BeFalse();
        _zeit.Advance(TimeSpan.FromMilliseconds(1));
        var spruch = await aufgabe.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        spruch.Quelle.Should().Be(SpruchQuelle.Ersatz);
        _speicher.Buchungen.Should().Equal((new DateOnly(2026, 11, 1), new LlmBuchung(600, 0, 1, 1, 0)));
    }

    [Fact]
    public async Task Abbruch_durch_den_Aufrufer_wird_weitergereicht()
    {
        _provider.Haengt = true;
        using var abbruch = new CancellationTokenSource();

        var aufgabe = Dienst().ErzeugeAsync(Anfrage(Partie()), abbruch.Token);
        await _provider.Gestartet.Task.WaitAsync(Ct);
        await abbruch.CancelAsync();

        await FluentActions.Awaiting(() => aufgabe).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Pause_nach_429_ueberspringt_Mistral()
    {
        _guard.Pausiert = true;

        (await Spruch(Partie())).Quelle.Should().Be(SpruchQuelle.Ersatz);
        _provider.Aufrufe.Should().Be(0);
    }

    [Fact]
    public async Task Ohne_Anbieter_nur_Ersatzsprueche()
    {
        _resolver.IsOff = true;

        (await Spruch(Partie())).Quelle.Should().Be(SpruchQuelle.Ersatz);
        _provider.Aufrufe.Should().Be(0);
    }

    [Fact]
    public async Task Partiebeginn_ist_immer_ein_Ersatzspruch()
    {
        var spruch = await Spruch(Partie(), SpruchEreignis.Partiebeginn);

        spruch.Quelle.Should().Be(SpruchQuelle.Ersatz);
        spruch.Text.Should().BeOneOf("Los geht's!", "Attacke von Anfang an!", "Ich bin bereit.");
        _provider.Aufrufe.Should().Be(0);
    }

    [Fact]
    public async Task Partieende_durch_den_Sweep_ist_ein_Ersatzspruch()
    {
        var partie = Partie();

        var spruch = await Dienst().ErzeugeAsync(Anfrage(partie, SpruchEreignis.Sieg) with { NurErsatz = true }, Ct);

        spruch.Quelle.Should().Be(SpruchQuelle.Ersatz);
        _provider.Aufrufe.Should().Be(0);
        partie.MistralAufrufe.Should().Be(0);
    }

    [Theory]
    [InlineData(false, 19, SpruchEreignis.Zug, SpruchQuelle.Mistral)]
    [InlineData(false, 20, SpruchEreignis.Zug, SpruchQuelle.Ersatz)]
    [InlineData(false, 20, SpruchEreignis.Sieg, SpruchQuelle.Mistral)]
    [InlineData(false, 25, SpruchEreignis.Niederlage, SpruchQuelle.Mistral)]
    [InlineData(false, 20, SpruchEreignis.Remis, SpruchQuelle.Mistral)]
    [InlineData(true, 11, SpruchEreignis.Zug, SpruchQuelle.Mistral)]
    [InlineData(true, 12, SpruchEreignis.Zug, SpruchQuelle.Ersatz)]
    [InlineData(true, 12, SpruchEreignis.Sieg, SpruchQuelle.Ersatz)]
    public async Task Deckel_je_Partie(bool gast, int bisher, SpruchEreignis ereignis, SpruchQuelle erwartet)
    {
        var partie = Partie(gast, mistralAufrufe: bisher);

        (await Spruch(partie, ereignis)).Quelle.Should().Be(erwartet);
        partie.MistralAufrufe.Should().Be(erwartet == SpruchQuelle.Mistral ? bisher + 1 : bisher);
    }

    [Theory]
    [InlineData(Bedenkzeit.Fischer5Plus3, 29_999L, SpruchQuelle.Ersatz)]
    [InlineData(Bedenkzeit.Fischer5Plus3, 30_000L, SpruchQuelle.Mistral)]
    [InlineData(Bedenkzeit.JeZug90s, 10_000L, SpruchQuelle.Ersatz)]
    [InlineData(Bedenkzeit.OhneUhr, null, SpruchQuelle.Mistral)]
    public async Task Knappe_Gegnerzeit_nimmt_den_Ersatzspruch(Bedenkzeit bedenkzeit, long? rest, SpruchQuelle erwartet) =>
        (await Spruch(Partie(bedenkzeit: bedenkzeit, gegnerRestMs: rest))).Quelle.Should().Be(erwartet);

    [Fact]
    public async Task Ausfall_des_Budgetwaechters_fuehrt_zum_Ersatzspruch()
    {
        _guard.Fehler = new InvalidOperationException("db weg");

        (await Spruch(Partie())).Quelle.Should().Be(SpruchQuelle.Ersatz);
    }

    [Fact]
    public async Task Ausfall_der_Buchung_schadet_dem_Spruch_nicht()
    {
        _speicher.BuchungWirft = true;

        (await Spruch(Partie())).Quelle.Should().Be(SpruchQuelle.Mistral);
    }

    [Fact]
    public async Task Ersatzsprueche_wiederholen_sich_nicht_in_der_Partie()
    {
        _resolver.IsOff = true;
        var partie = Partie();
        var gesagt = new List<string>();

        for (var halbzug = 2; halbzug <= 8; halbzug += 2)
        {
            var anfrage = Anfrage(partie) with { NachHalbzug = halbzug, BisherigeTexte = [.. gesagt] };
            gesagt.Add((await Dienst().ErzeugeAsync(anfrage, Ct)).Text);
        }

        gesagt.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Erschoepfte_Sprachausgabe_laesst_Live_Sprueche_unberuehrt()
    {
        // Eigenes Budget (SCHACH-SPEC 6.9): die Sprachausgabe ist leer, die Chat-Leiter nicht.
        var tts = new Sprachausgabe.FakeSprachVerbrauch();
        tts.Zeichen[new DateOnly(2026, 11, 1)] = 10_000_000;
        var stimmen = new Sprachausgabe.FakeSprachProvider();
        var sprachausgabe = new SprachausgabeDienst(
            stimmen, new Sprachausgabe.FakeStimmeSpeicher(), tts, new Sprachausgabe.FakeSiegel(), new Sprachausgabe.FakeWarner(),
            new SprachausgabeOptions { Stimmen = new() { ["finn"] = new StimmeOptions { VoiceId = "v" } } },
            _zeit, NullLogger<SprachausgabeDienst>.Instance);
        var echterGuard = new LlmBudgetGuard(_speicher, _resolver, new LlmBudgetOptions(), _zeit);
        var dienst = new SpruchDienst(
            _resolver, echterGuard, _speicher,
            new ErsatzKatalog(new Dictionary<string, IReadOnlyDictionary<SpruchEreignis, IReadOnlyList<string>>>
            {
                ["finn"] = ErsatzDatei.Parse(ErsatzKatalogTests.Gueltig, "finn.de.md"),
            }),
            new FestePersona(), _optionen, _zeit, NullLogger<SpruchDienst>.Instance);

        var ton = await sprachausgabe.AudioAsync(sprachausgabe.Token("finn", "Hallo", gast: false)!, Ct);
        var spruch = await dienst.ErzeugeAsync(Anfrage(Partie(gast: true)), Ct);

        ton.Ausgang.Should().Be(SprachAusgang.Stumm);
        stimmen.Anfragen.Should().BeEmpty();
        (await echterGuard.GetStatusAsync(Ct)).Level.Should().Be(LlmBudgetLevel.Ok);
        spruch.Quelle.Should().Be(SpruchQuelle.Mistral);
        _provider.Aufrufe.Should().Be(1);
    }

    private sealed class FestePersona : IPersonaQuelle
    {
        public string Persona(string gegnerKey) => "Persona " + gegnerKey;
    }

    private sealed class FakeGuard : ILlmBudgetGuard
    {
        public LlmBudgetLevel Level { get; set; } = LlmBudgetLevel.Ok;

        public Exception? Fehler { get; set; }

        public bool Pausiert { get; set; }

        public bool Gemeldet429 { get; private set; }

        public bool IstPausiert => Pausiert;

        public Task<LlmBudgetStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Fehler is null ? Task.FromResult(new LlmBudgetStatus(0, 1, Level)) : Task.FromException<LlmBudgetStatus>(Fehler);

        public void Melde429() => Gemeldet429 = true;
    }

    private sealed class FakeProvider : ILlmProvider
    {
        public string Antwort { get; set; } = "Attacke, mein Springer!";

        public Exception? Fehler { get; set; }

        public bool Haengt { get; set; }

        public int Aufrufe { get; private set; }

        public LlmRequest? LetzteAnfrage { get; private set; }

        public TaskCompletionSource Gestartet { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Aufrufe++;
            LetzteAnfrage = request;
            Gestartet.TrySetResult();
            if (Haengt)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return Fehler is null ? new LlmCompletion(Antwort, 400, 20) : throw Fehler;
        }
    }
}
