using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Schach.Application.Betrieb;
using Schach.Application.Sprachausgabe;

namespace Schach.Application.Tests.Sprachausgabe;

/// <summary>Sprachausgabe (SCHACH-SPEC 6.9): Token, Cache, eigenes Budget, jeder Fehler ergibt Stille.</summary>
public sealed class SprachausgabeDienstTests
{
    private readonly FakeTimeProvider _zeit = new(new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeSprachProvider _provider = new();
    private readonly FakeStimmeSpeicher _cache = new();
    private readonly FakeSprachVerbrauch _verbrauch = new();
    private readonly FakeWarner _warner = new();
    private readonly SprachausgabeOptions _optionen = new()
    {
        Stimmen = new(StringComparer.OrdinalIgnoreCase)
        {
            ["oscar"] = new StimmeOptions { VoiceId = "voice_oscar", Expressiveness = 2.5 },
            ["finn"] = new StimmeOptions { VoiceId = "" },
        },
    };

    private SprachausgabeDienst? _dienst;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SprachausgabeDienst Dienst() => _dienst ??= new(
        _provider, _cache, _verbrauch, new FakeSiegel(), _warner, _optionen, _zeit, NullLogger<SprachausgabeDienst>.Instance);

    private string Token(string text = "Schach! Das hast du nicht kommen sehen.", bool gast = false) =>
        Dienst().Token("oscar", text, gast)!;

    private static readonly DateOnly Monat = new(2026, 11, 1);

    [Fact]
    public async Task Token_liefert_den_Ton_des_Gegners_fuer_genau_diesen_Text()
    {
        var ton = await Dienst().AudioAsync(Token("Na dann!"), Ct);

        ton.Ausgang.Should().Be(SprachAusgang.Audio);
        System.Text.Encoding.UTF8.GetString(ton.Audio!).Should().Be("mp3:voice_oscar:Na dann!");
        _provider.Anfragen.Should().ContainSingle().Which.Should().Be(new SprachAnfrage("voxtral-mini-tts-3-0", "voice_oscar", 2.5, "Na dann!"));
    }

    [Fact]
    public async Task Zweiter_Abruf_kommt_aus_dem_Cache_ohne_Mistral()
    {
        await Dienst().AudioAsync(Token(), Ct);

        var zweiter = await Dienst().AudioAsync(Token(), Ct);

        zweiter.Ausgang.Should().Be(SprachAusgang.Audio);
        _provider.Anfragen.Should().ContainSingle();
        _verbrauch.Buchungen.Should().ContainSingle();
    }

    [Fact]
    public void Ohne_VoiceId_oder_Gegner_ist_die_Stimme_null()
    {
        Dienst().Token("finn", "Hallo", gast: false).Should().BeNull();
        Dienst().Token("ilse", "Hallo", gast: false).Should().BeNull();
    }

    [Fact]
    public void Ohne_Anbieter_oder_ausgeschaltet_ist_die_Stimme_null()
    {
        _provider.Verfuegbar = false;
        Dienst().Token("oscar", "Hallo", gast: false).Should().BeNull();

        _provider.Verfuegbar = true;
        _optionen.Aktiv = false;
        Dienst().Token("oscar", "Hallo", gast: false).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("kaputt")]
    [InlineData("ok.%%%")]
    public async Task Ungueltiges_Token_ergibt_Ungueltig(string token)
    {
        var ton = await Dienst().AudioAsync(token, Ct);

        ton.Ausgang.Should().Be(SprachAusgang.Ungueltig);
        _provider.Anfragen.Should().BeEmpty();
    }

    [Fact]
    public async Task Gefaelschter_Inhalt_im_gueltigen_Siegel_ist_ungueltig()
    {
        var token = new FakeSiegel().Versiegeln("kein json");

        (await Dienst().AudioAsync(token, Ct)).Ausgang.Should().Be(SprachAusgang.Ungueltig);
    }

    [Fact]
    public async Task Erschoepftes_Budget_ergibt_Stille_und_eine_Meldung()
    {
        // 500 Cent bei 1300 Cent je Million Zeichen: ab rund 384 616 Zeichen erschoepft.
        _verbrauch.Zeichen[Monat] = 400_000;

        var ton = await Dienst().AudioAsync(Token(), Ct);

        ton.Ausgang.Should().Be(SprachAusgang.Stumm);
        _provider.Anfragen.Should().BeEmpty();
        _warner.Meldungen.Should().Contain(BetriebsWarnung.SprachausgabeBudgetErschoepft);
    }

    [Fact]
    public async Task Erschoepftes_Budget_liefert_trotzdem_den_Cache()
    {
        await Dienst().AudioAsync(Token(), Ct);
        _verbrauch.Zeichen[Monat] = 400_000;
        _zeit.Advance(TimeSpan.FromMinutes(5));

        (await Dienst().AudioAsync(Token(), Ct)).Ausgang.Should().Be(SprachAusgang.Audio);
    }

    [Theory]
    [InlineData(false, SprachAusgang.Audio)]
    [InlineData(true, SprachAusgang.Stumm)]
    public async Task Warnstufe_Gaeste_ohne_Stimme(bool gast, SprachAusgang erwartet)
    {
        _verbrauch.Zeichen[Monat] = 320_000; // gut 83 %

        var ton = await Dienst().AudioAsync(Token(gast: gast), Ct);

        ton.Ausgang.Should().Be(erwartet);
        _warner.Meldungen.Should().Contain(BetriebsWarnung.SprachausgabeBudgetWarn);
    }

    [Fact]
    public void Budget_null_schaltet_die_Stimme_ab()
    {
        _optionen.MonthlyBudgetEuroCents = 0;

        Dienst().Token("oscar", "Hallo", gast: false).Should().BeNull();
    }

    [Fact]
    public async Task Erfolg_bucht_die_Zeichen()
    {
        await Dienst().AudioAsync(Token("Zwölf Zeichen"), Ct);

        _verbrauch.Buchungen.Should().Equal(new SprachBuchung(13, 1, 0, 0));
    }

    [Fact]
    public async Task Status_429_pausiert_die_Sprachausgabe()
    {
        _provider.Fehler = HttpStatusCode.TooManyRequests;

        (await Dienst().AudioAsync(Token("Eins"), Ct)).Ausgang.Should().Be(SprachAusgang.Stumm);
        _provider.Fehler = null;
        (await Dienst().AudioAsync(Token("Zwei"), Ct)).Ausgang.Should().Be(SprachAusgang.Stumm);
        _provider.Anfragen.Should().ContainSingle("die Pause haelt den zweiten Aufruf zurueck");

        _zeit.Advance(TimeSpan.FromSeconds(61));
        (await Dienst().AudioAsync(Token("Zwei"), Ct)).Ausgang.Should().Be(SprachAusgang.Audio);
    }

    [Fact]
    public async Task Unbekannte_Stimme_ergibt_Stille_und_eine_Meldung()
    {
        _provider.Fehler = HttpStatusCode.NotFound;

        var ton = await Dienst().AudioAsync(Token(), Ct);

        ton.Ausgang.Should().Be(SprachAusgang.Stumm);
        _warner.Meldungen.Should().Equal(BetriebsWarnung.StimmeUnbekannt);
        _verbrauch.Buchungen.Should().Equal(new SprachBuchung(0, 1, 0, 1));
    }

    [Fact]
    public async Task Zeitlimit_ergibt_Stille_und_bucht_die_Zeichen()
    {
        _provider.Haengt = true;
        _provider.BeimAufruf = () => _zeit.Advance(TimeSpan.FromMilliseconds(_optionen.ZeitlimitMs + 1));

        var ton = await Dienst().AudioAsync(Token("Hallo"), Ct);

        ton.Ausgang.Should().Be(SprachAusgang.Stumm);
        _verbrauch.Buchungen.Should().Equal(new SprachBuchung(5, 1, 1, 0));
        _cache.Eintraege.Should().BeEmpty();
    }

    [Fact]
    public async Task Datenbankfehler_beim_Lesen_ergibt_Stille()
    {
        _cache.LesenWirft = true;

        (await Dienst().AudioAsync(Token(), Ct)).Ausgang.Should().Be(SprachAusgang.Stumm);
        _provider.Anfragen.Should().BeEmpty();
    }

    [Fact]
    public async Task Datenbankfehler_beim_Speichern_liefert_den_Ton_trotzdem()
    {
        _cache.SpeichernWirft = true;

        var ton = await Dienst().AudioAsync(Token("Hallo"), Ct);

        ton.Ausgang.Should().Be(SprachAusgang.Audio);
        System.Text.Encoding.UTF8.GetString(ton.Audio!).Should().Be("mp3:voice_oscar:Hallo");
    }

    [Fact]
    public async Task Aufraeumen_loescht_nach_90_Tagen_ohne_Nutzung()
    {
        await Dienst().AudioAsync(Token("Alt"), Ct);
        _zeit.Advance(TimeSpan.FromDays(60));
        await Dienst().AudioAsync(Token("Neu"), Ct);
        _zeit.Advance(TimeSpan.FromDays(31));

        var geloescht = await Dienst().AufraeumenAsync(Ct);

        geloescht.Should().Be(1);
        _cache.Eintraege.Should().ContainSingle();
    }
}
