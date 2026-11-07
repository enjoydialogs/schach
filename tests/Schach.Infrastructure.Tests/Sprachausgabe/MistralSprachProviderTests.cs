using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Sprachausgabe;
using Schach.Infrastructure.Llm;
using Schach.Infrastructure.Sprachausgabe;

namespace Schach.Infrastructure.Tests.Sprachausgabe;

/// <summary>SCHACH-SPEC 6.9: Aufrufform gegen Voxtral TTS, Token-Siegel, DI. Nie gegen die echte API.</summary>
public sealed class MistralSprachProviderTests
{
    private const string ErfundenerSchluessel = "placeholder";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (MistralSprachProvider Provider, StubHandler Handler) Provider(HttpStatusCode status, string antwort)
    {
        var handler = new StubHandler(status, antwort);
        var llm = new LlmOptions { ApiKey = ErfundenerSchluessel };
        var optionen = new SprachausgabeOptions();
        return (new MistralSprachProvider(new EinClient(handler, llm, optionen), llm, optionen), handler);
    }

    [Fact]
    public async Task Ruft_den_EU_Endpunkt_mit_voice_id_und_mp3()
    {
        var (provider, handler) = Provider(HttpStatusCode.OK, """{"audio_data":"SUQz"}""");

        var audio = await provider.SprichAsync(new SprachAnfrage("voxtral-mini-tts-3-0", "voice_1", 2.5, "Schach!"), Ct);

        audio.Should().Equal(Encoding.ASCII.GetBytes("ID3"));
        handler.Uri.Should().Be(new Uri("https://api.eu.mistral.ai/v1/audio/speech"));
        handler.Auth.Should().Be("Bearer " + ErfundenerSchluessel);
        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        root.GetProperty("model").GetString().Should().Be("voxtral-mini-tts-3-0");
        root.GetProperty("input").GetString().Should().Be("Schach!");
        root.GetProperty("voice_id").GetString().Should().Be("voice_1");
        root.GetProperty("response_format").GetString().Should().Be("mp3");
        root.GetProperty("expressiveness").GetDouble().Should().Be(2.5);
        root.TryGetProperty("stream", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Ohne_Expressiveness_fehlt_das_Feld()
    {
        var (provider, handler) = Provider(HttpStatusCode.OK, """{"audio_data":"SUQz"}""");

        await provider.SprichAsync(new SprachAnfrage("voxtral-mini-tts-3-0", "voice_1", null, "Hallo"), Ct);

        JsonDocument.Parse(handler.Body!).RootElement.TryGetProperty("expressiveness", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Fehlerstatus_wirft_mit_Statuscode(HttpStatusCode status)
    {
        var (provider, _) = Provider(status, """{"message":"Voice 'x' not found."}""");

        var act = () => provider.SprichAsync(new SprachAnfrage("m", "x", null, "Hallo"), Ct);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(status);
    }

    [Fact]
    public async Task Antwort_ohne_audio_data_wirft()
    {
        var (provider, _) = Provider(HttpStatusCode.OK, "{}");

        var act = () => provider.SprichAsync(new SprachAnfrage("m", "x", null, "Hallo"), Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("OpenAiCompatible", ErfundenerSchluessel, true)]
    [InlineData("OpenAiCompatible", "", false)]
    [InlineData("DeterministicFallback", ErfundenerSchluessel, false)]
    public void Verfuegbar_nur_mit_Anbieter_und_Schluessel(string anbieter, string schluessel, bool erwartet)
    {
        var llm = new LlmOptions { Provider = anbieter, ApiKey = schluessel };

        new MistralSprachProvider(new EinClient(new StubHandler(HttpStatusCode.OK, "{}"), llm, new()), llm, new()).Verfuegbar.Should().Be(erwartet);
    }

    [Fact]
    public void Siegel_oeffnet_nur_eigene_Tokens_und_ist_url_tauglich()
    {
        var siegel = new StimmSiegel(new EphemeralDataProtectionProvider());

        var token = siegel.Versiegeln("""{"g":"oscar","t":"Schach!","a":true}""");

        token.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        siegel.Oeffnen(token).Should().Be("""{"g":"oscar","t":"Schach!","a":true}""");
        siegel.Oeffnen(token[..^2] + (token[^2] == 'A' ? "BB" : "AA")).Should().BeNull();
        siegel.Oeffnen("%%%").Should().BeNull();
        new StimmSiegel(new EphemeralDataProtectionProvider()).Oeffnen(token).Should().BeNull();
    }

    [Fact]
    public async Task Registrierung_bindet_Stimmen_aus_der_Konfiguration()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Llm:ApiKey"] = ErfundenerSchluessel,
            ["Sprachausgabe:Stimmen:oscar:VoiceId"] = "voice_oscar",
            ["Sprachausgabe:Stimmen:oscar:Expressiveness"] = "3",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton(TimeProvider.System);
        services.AddDataProtection();
        services.AddSingleton<Application.Betrieb.IBetriebsWarner, StillerWarner>();
        services.AddSprachausgabe(config);
        await using var sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var stimmen = sp.GetRequiredService<ISpruchStimmen>();

        stimmen.Token("oscar", "Hallo", gast: false).Should().NotBeNull();
        stimmen.Token("finn", "Hallo", gast: false).Should().BeNull();
        sp.GetRequiredService<SprachausgabeOptions>().Stimmen["oscar"].Expressiveness.Should().Be(3);
        sp.GetRequiredService<ISpruchStimmen>().Should().BeSameAs(sp.GetRequiredService<SprachausgabeDienst>());
    }

    private sealed class StillerWarner : Application.Betrieb.IBetriebsWarner
    {
        public bool Melden(Application.Betrieb.BetriebsWarnung art, string? zusatz = null) => true;
    }

    private sealed class EinClient(HttpMessageHandler handler, LlmOptions llm, SprachausgabeOptions optionen) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            MistralSprachProvider.KonfiguriereClient(new HttpClient(handler, disposeHandler: false), llm, optionen);
    }

    private sealed class StubHandler(HttpStatusCode status, string antwort) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public string? Auth { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Auth = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(antwort, Encoding.UTF8, "application/json") };
        }
    }
}
