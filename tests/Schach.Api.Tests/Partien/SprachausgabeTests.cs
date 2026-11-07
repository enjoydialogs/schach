using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Schach.Api.Contracts;
using Schach.Api.Tests.Konto;
using Schach.Application.Llm;
using Schach.Application.Sprachausgabe;
using Schach.Tests.Shared;

namespace Schach.Api.Tests.Partien;

/// <summary><c>GET /sprachausgabe/{token}</c> ueber HTTP (SCHACH-SPEC 6.9, Issue #77).</summary>
public sealed class SprachausgabeTests
{
    private static readonly Dictionary<string, string> MitStimme = new()
    {
        ["Sprachausgabe:Stimmen:finn:VoiceId"] = "voice_finn",
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<ZustandDto> ZiehenAsync(ApiSitzung browser, ZustandDto zustand, string uci)
    {
        using var antwort = await browser.PostAsync($"/api/v1/partien/{zustand.Id}/zuege", new ZugAnfrage(zustand.Halbzug, uci));
        antwort.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await antwort.Content.ReadFromJsonAsync<ZustandDto>(ApiSitzung.Json, Ct))!;
    }

    private static async Task<ZustandDto> StartenAsync(ApiSitzung browser, string gegner)
    {
        using var antwort = await browser.PostAsync("/api/v1/partien", new NeuePartieAnfrage(gegner, FarbeWahl.Weiss, Bedenkzeit.OhneUhr));
        antwort.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await antwort.Content.ReadFromJsonAsync<ZustandDto>(ApiSitzung.Json, Ct))!;
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Schlussspruch_einer_geloeschten_Gast_Partie_klingt_und_kommt_dann_aus_dem_Cache() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        factory.Engine.Vorgabe("e7e5", "d8h4");
        var zustand = await StartenAsync(gast, "finn");
        zustand = await ZiehenAsync(gast, zustand, "f2f3");
        zustand = await ZiehenAsync(gast, zustand, "g2g4");
        zustand.Status.Should().Be(PartieStatus.Beendet);
        using (var weg = await gast.Http.GetAsync(new Uri($"/api/v1/partien/{zustand.Id}", UriKind.Relative), Ct))
        {
            weg.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        var schluss = zustand.Sprueche[^1];
        schluss.Ereignis.Should().Be(SpruchEreignis.Sieg);
        schluss.Stimme.Should().NotBeNullOrEmpty();

        using var ton = await gast.Http.GetAsync(new Uri($"/api/v1/sprachausgabe/{schluss.Stimme}", UriKind.Relative), Ct);
        ton.StatusCode.Should().Be(HttpStatusCode.OK);
        ton.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
        Encoding.UTF8.GetString(await ton.Content.ReadAsByteArrayAsync(Ct)).Should().Be($"mp3:voice_finn:{schluss.Text}");

        using var nochmal = await gast.Http.GetAsync(new Uri($"/api/v1/sprachausgabe/{schluss.Stimme}", UriKind.Relative), Ct);
        nochmal.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.Stimme.Aufrufe.Should().Be(1);
    }, MitStimme);

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Ungueltiges_Token_ist_404_und_Gegner_ohne_Stimme_hat_null() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);

        var zustand = await StartenAsync(gast, "oscar");
        using var falsch = await gast.Http.GetAsync(new Uri("/api/v1/sprachausgabe/CfDJ8kaputt", UriKind.Relative), Ct);

        // Ausdrucksbaum: `is null` geht hier nicht (CS8122).
        zustand.Sprueche.Should().OnlyContain(s => s.Stimme == null);
        falsch.StatusCode.Should().Be(HttpStatusCode.NotFound);
        factory.Stimme.Aufrufe.Should().Be(0);
    }, MitStimme);

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Erschoepftes_Budget_ergibt_204() => await AuthAblaufTests.MitApiAsync(async factory =>
    {
        using var gast = new ApiSitzung(factory);
        var zustand = await StartenAsync(gast, "finn");
        var token = zustand.Sprueche.Single().Stimme!;
        // 1 Mio. Zeichen kosten 13 EUR, weit ueber dem Vorgabe-Budget von 5 EUR.
        await factory.Services.GetRequiredService<ISprachVerbrauchSpeicher>()
            .BucheAsync(LlmMonat.Von(DateTimeOffset.UtcNow), new SprachBuchung(1_000_000, 0, 0, 0), Ct);

        using var ton = await gast.Http.GetAsync(new Uri($"/api/v1/sprachausgabe/{token}", UriKind.Relative), Ct);

        ton.StatusCode.Should().Be(HttpStatusCode.NoContent);
        factory.Stimme.Aufrufe.Should().Be(0);
    }, MitStimme);
}
