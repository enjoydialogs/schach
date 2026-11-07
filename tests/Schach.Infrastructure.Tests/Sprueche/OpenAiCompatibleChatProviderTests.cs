using System.Net;
using System.Text;
using System.Text.Json;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Llm;

namespace Schach.Infrastructure.Tests.Sprueche;

/// <summary>SCHACH-SPEC 6.3: Aufrufform gegen Mistral. Nie gegen die echte API.</summary>
public sealed class OpenAiCompatibleChatProviderTests
{
    private const string ErfundenerSchluessel = "placeholder";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (OpenAiCompatibleChatProvider Provider, StubHandler Handler) Provider(HttpStatusCode status, string antwort)
    {
        var handler = new StubHandler(status, antwort);
        var optionen = new LlmOptions { ApiKey = ErfundenerSchluessel };
        var http = OpenAiCompatibleChatProvider.KonfiguriereClient(new HttpClient(handler), optionen);
        return (new OpenAiCompatibleChatProvider(http, new FesterKatalog(), optionen), handler);
    }

    private static LlmRequest Anfrage => new("spruch.de.md", new Dictionary<string, string> { ["name"] = "Finn" }, 80, 0.8);

    private static string Antwort(string contentJson) =>
        $$$"""{"choices":[{"message":{"role":"assistant","content":{{{contentJson}}}}}],"usage":{"prompt_tokens":412,"completion_tokens":17}}""";

    [Fact]
    public async Task Ruft_den_EU_Endpunkt_mit_den_Parametern_aus_der_Spec()
    {
        var (provider, handler) = Provider(HttpStatusCode.OK, Antwort("\"Attacke!\""));

        await provider.CompleteAsync(Anfrage, Ct);

        handler.Uri.Should().Be(new Uri("https://api.eu.mistral.ai/v1/chat/completions"));
        handler.Methode.Should().Be(HttpMethod.Post);
        handler.Auth.Should().Be("Bearer " + ErfundenerSchluessel);

        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        root.GetProperty("model").GetString().Should().Be("ministral-8b-2512");
        root.GetProperty("temperature").GetDouble().Should().Be(0.8);
        root.GetProperty("max_tokens").GetInt32().Should().Be(80);
        root.GetProperty("messages").GetArrayLength().Should().Be(1);
        root.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("user");
        root.GetProperty("messages")[0].GetProperty("content").GetString().Should().Be("Prompt fuer Finn");
        root.TryGetProperty("reasoning_effort", out _).Should().BeFalse();
        root.TryGetProperty("stream", out _).Should().BeFalse();
        root.TryGetProperty("top_p", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Liest_content_als_Zeichenkette_und_die_Tokenzahlen()
    {
        var (provider, _) = Provider(HttpStatusCode.OK, Antwort("\"Attacke!\""));

        var ergebnis = await provider.CompleteAsync(Anfrage, Ct);

        ergebnis.Should().Be(new LlmCompletion("Attacke!", 412, 17));
    }

    [Fact]
    public async Task Liest_nur_die_text_Teile_einer_Liste()
    {
        var (provider, _) = Provider(HttpStatusCode.OK, Antwort("""
            [{"type":"thinking","thinking":"hm"},{"type":"text","text":"Boah, "},{"type":"text","text":"Schach!"}]
            """));

        (await provider.CompleteAsync(Anfrage, Ct)).Text.Should().Be("Boah, Schach!");
    }

    [Fact]
    public async Task Liste_ohne_text_Teil_wirft()
    {
        var (provider, _) = Provider(HttpStatusCode.OK, Antwort("""[{"type":"thinking","thinking":"hm"}]"""));

        var act = () => provider.CompleteAsync(Anfrage, Ct);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Fehlerstatus_wirft_mit_Statuscode(HttpStatusCode status)
    {
        var (provider, _) = Provider(status, """{"message":"nope"}""");

        var act = () => provider.CompleteAsync(Anfrage, Ct);

        (await act.Should().ThrowAsync<HttpRequestException>()).Which.StatusCode.Should().Be(status);
    }

    [Fact]
    public void HttpClient_Timeout_5_Sekunden()
    {
        var http = OpenAiCompatibleChatProvider.KonfiguriereClient(new HttpClient(), new LlmOptions());

        http.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("OpenAiCompatible", "placeholder", false)]
    [InlineData("OpenAiCompatible", "", true)]
    [InlineData("DeterministicFallback", "placeholder", true)]
    [InlineData("Tippfehler", "placeholder", true)]
    public void Resolver_ist_aus_ohne_Schluessel_oder_Anbieter(string anbieter, string schluessel, bool aus)
    {
        var resolver = new LlmProviderResolver(null!, new LlmOptions { Provider = anbieter, ApiKey = schluessel });

        resolver.IsOff.Should().Be(aus);
        resolver.ModelName.Should().Be(aus ? "" : "ministral-8b-2512");
    }

    private sealed class FesterKatalog : IPromptCatalog
    {
        public string Render(string promptFile, IReadOnlyDictionary<string, string> variables) => "Prompt fuer " + variables["name"];
    }

    private sealed class StubHandler(HttpStatusCode status, string antwort) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public HttpMethod? Methode { get; private set; }

        public string? Auth { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Methode = request.Method;
            Auth = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(antwort, Encoding.UTF8, "application/json") };
        }
    }
}
