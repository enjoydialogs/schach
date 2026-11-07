using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Email;

namespace Schach.Infrastructure.Tests.Email;

/// <summary>Versand ueber die Lettermint-HTTP-API (SCHACH-SPEC 9.6, 13.3): Tracking aus, wirft nie.</summary>
public sealed class LettermintEmailSenderTests
{
    private static readonly OutboundEmail Mail = new(
        "anna@example.de", "Anmelden bei Schach", "Text", "<p>Html</p>", "kontakt@enjoy-dialogs.de");

    private static readonly EmailOptionen Optionen = new()
    {
        FromAddress = "Schach <anmeldung@schach.enjoy-dialogs.de>",
        Lettermint = new LettermintOptionen { ApiToken = "test-token", BaseUrl = "https://api.lettermint.test/v1/" },
    };

    [Fact]
    public async Task Sendet_POST_send_mit_Token_Header_und_Tracking_aus()
    {
        var handler = new AufzeichnenderHandler(HttpStatusCode.Accepted, """{"message_id":"m1","status":"pending"}""");
        var sender = Erzeuge(handler);

        var ok = await sender.SendAsync(Mail, TestContext.Current.CancellationToken);

        ok.Should().BeTrue();
        handler.Anfrage!.Method.Should().Be(HttpMethod.Post);
        handler.Anfrage.RequestUri.Should().Be(new Uri("https://api.lettermint.test/v1/send"));
        handler.Anfrage.Headers.GetValues("x-lettermint-token").Should().ContainSingle("test-token");

        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        root.GetProperty("from").GetString().Should().Be("Schach <anmeldung@schach.enjoy-dialogs.de>");
        root.GetProperty("to").EnumerateArray().Select(e => e.GetString()).Should().Equal("anna@example.de");
        root.GetProperty("reply_to").EnumerateArray().Select(e => e.GetString()).Should().Equal("kontakt@enjoy-dialogs.de");
        root.GetProperty("subject").GetString().Should().Be("Anmelden bei Schach");
        root.GetProperty("text").GetString().Should().Be("Text");
        root.GetProperty("html").GetString().Should().Be("<p>Html</p>");
        root.GetProperty("settings").GetProperty("track_opens").GetBoolean().Should().BeFalse();
        root.GetProperty("settings").GetProperty("track_clicks").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Fehlerstatus_ergibt_false(HttpStatusCode status)
    {
        var sender = Erzeuge(new AufzeichnenderHandler(status, "{}"));

        (await sender.SendAsync(Mail, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Transportfehler_wirft_nicht_sondern_ergibt_false()
    {
        var sender = Erzeuge(new AufzeichnenderHandler(null, ""));

        (await sender.SendAsync(Mail, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task Ohne_Token_wird_nichts_gesendet()
    {
        var handler = new AufzeichnenderHandler(HttpStatusCode.Accepted, "{}");
        var sender = new LettermintEmailSender(
            new HttpClient(handler),
            Options.Create(new EmailOptionen { FromAddress = "a@b.de" }),
            NullLogger<LettermintEmailSender>.Instance);

        (await sender.SendAsync(Mail, TestContext.Current.CancellationToken)).Should().BeFalse();
        handler.Anfrage.Should().BeNull();
    }

    private static LettermintEmailSender Erzeuge(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(Optionen), NullLogger<LettermintEmailSender>.Instance);

    private sealed class AufzeichnenderHandler(HttpStatusCode? status, string antwort) : HttpMessageHandler
    {
        public HttpRequestMessage? Anfrage { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Anfrage = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (status is null)
            {
                throw new HttpRequestException("verbindung weg");
            }

            return new HttpResponseMessage(status.Value) { Content = new StringContent(antwort) };
        }
    }
}
