using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Schach.Api.RateLimiting;
using Schach.Infrastructure.Telemetry;
using Schach.Tests.Shared;

namespace Schach.Api.Tests;

/// <summary>
/// Rate-Limiter nach SCHACH-SPEC 11.4 in der Pipeline <c>UseRouting → UseRateLimiter</c>, ohne
/// Datenbank. Die Endpunkte hier sind Platzhalter mit den echten Routenformen; die Controller kommen
/// mit S1c/S1d/S2a und haengen dieselben Policies an.
/// </summary>
public sealed class RateLimiterTests : IAsyncDisposable
{
    private const string TestIp = "X-Test-Ip";
    private const string TestAngemeldet = "X-Test-Angemeldet";

    private readonly Messwerte _messwerte = new();
    private readonly TaskCompletionSource _tippGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _tippErreicht = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private WebApplication? _app;
    private bool _zuegeBlockieren;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<HttpClient> ClientAsync(Dictionary<string, string?> limits)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(limits.ToDictionary(kv => "Limits:" + kv.Key, kv => kv.Value));
        builder.Services.AddSingleton(new SchachZaehler(_messwerte.Factory));
        builder.Services.AddSchachRateLimiter(builder.Configuration);

        var app = builder.Build();
        // TestServer kennt keine Client-Adresse und keine Anmeldung: beides setzt der Test per Header.
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue(TestIp, out var ip))
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
            }

            if (context.Request.Headers.ContainsKey(TestAngemeldet))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "s")], "Test"));
            }

            return next(context);
        });
        app.UseRouting();
        app.UseRateLimiter();
        app.MapPost("/api/v1/partien", () => Results.Created()).RequireRateLimiting(SchachRateLimits.GastPartieJeIp);
        app.MapPost("/api/v1/partien/{id}/zuege", async () =>
        {
            if (_zuegeBlockieren)
            {
                _tippErreicht.TrySetResult();
                await _tippGate.Task;
            }

            return Results.Ok();
        }).RequireRateLimiting(SchachRateLimits.ZuegeJePartie);
        app.MapPost("/api/v1/partien/{id}/tipp", async () =>
        {
            _tippErreicht.TrySetResult();
            await _tippGate.Task;
            return Results.Ok();
        }).RequireRateLimiting(SchachRateLimits.TippJePartie);
        app.MapPost("/api/v1/partien/{id}/aufgeben", () => Results.Ok()).RequireRateLimiting(SchachRateLimits.AktionJePartie);
        app.MapPost("/api/v1/auth/anmeldung", () => Results.Accepted()).RequireRateLimiting(SchachRateLimits.MagicLinkJeIp);
        await app.StartAsync(Ct);
        _app = app;
        return app.GetTestClient();
    }

    private static HttpRequestMessage Post(string pfad, string? ip = null, bool angemeldet = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(pfad, UriKind.Relative));
        if (ip is not null)
        {
            request.Headers.Add(TestIp, ip);
        }

        if (angemeldet)
        {
            request.Headers.Add(TestAngemeldet, "1");
        }

        return request;
    }

    private static async Task<HttpStatusCode> StatusAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request)
        {
            using var response = await client.SendAsync(request, Ct);
            return response.StatusCode;
        }
    }

    private IEnumerable<string?> AbgelehntePolicies => _messwerte.Von(SchachMetrics.LimitsAbgelehnt).Select(t => t["policy"]);

    [Fact]
    public async Task Zuege_sind_je_Partie_begrenzt_und_429_traegt_Retry_After()
    {
        var client = await ClientAsync(new() { ["ZuegeJePartieUndMinute"] = "2" });
        var id = Guid.NewGuid();

        (await StatusAsync(client, Post($"/api/v1/partien/{id}/zuege"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(client, Post($"/api/v1/partien/{id}/zuege"))).Should().Be(HttpStatusCode.OK);
        using var abgelehnt = await client.SendAsync(Post($"/api/v1/partien/{id}/zuege"), Ct);

        abgelehnt.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        abgelehnt.Headers.RetryAfter!.Delta!.Value.Should().BePositive();
        (await StatusAsync(client, Post($"/api/v1/partien/{Guid.NewGuid()}/zuege"))).Should().Be(HttpStatusCode.OK, "jede Partie hat ihr eigenes Fenster");
        AbgelehntePolicies.Should().Equal(SchachRateLimits.ZuegeJePartie);
    }

    [Fact]
    public async Task Tipps_und_Aktionen_haben_eigene_Fenster_je_Partie()
    {
        var client = await ClientAsync(new() { ["TippsJePartieUndMinute"] = "1", ["AktionenJePartieUndMinute"] = "1" });
        _tippGate.SetResult();
        var id = Guid.NewGuid();

        (await StatusAsync(client, Post($"/api/v1/partien/{id}/tipp"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(client, Post($"/api/v1/partien/{id}/tipp"))).Should().Be(HttpStatusCode.TooManyRequests);
        (await StatusAsync(client, Post($"/api/v1/partien/{id}/aufgeben"))).Should().Be(HttpStatusCode.OK);
        (await StatusAsync(client, Post($"/api/v1/partien/{id}/aufgeben"))).Should().Be(HttpStatusCode.TooManyRequests);

        AbgelehntePolicies.Should().Equal(SchachRateLimits.TippJePartie, SchachRateLimits.AktionJePartie);
    }

    [Fact]
    public async Task Gast_Partien_sind_je_IP_begrenzt_ohne_IP_im_Meter()
    {
        var client = await ClientAsync(new() { ["GastPartienJeIpUndStunde"] = "1" });
        var a = IPAddress.Loopback.ToString();
        var b = IPAddress.IPv6Loopback.ToString();

        (await StatusAsync(client, Post("/api/v1/partien", a))).Should().Be(HttpStatusCode.Created);
        (await StatusAsync(client, Post("/api/v1/partien", a))).Should().Be(HttpStatusCode.TooManyRequests);
        (await StatusAsync(client, Post("/api/v1/partien", b))).Should().Be(HttpStatusCode.Created);

        var messung = _messwerte.Von(SchachMetrics.LimitsAbgelehnt).Should().ContainSingle().Subject;
        messung.Should().ContainKey("policy").WhoseValue.Should().Be(SchachRateLimits.GastPartieJeIp);
        messung.Keys.Should().Equal("policy");
    }

    [Fact]
    public async Task Angemeldete_Spieler_haben_kein_Gast_Partien_Limit()
    {
        var client = await ClientAsync(new() { ["GastPartienJeIpUndStunde"] = "1" });
        var ip = IPAddress.Loopback.ToString();

        for (var i = 0; i < 3; i++)
        {
            (await StatusAsync(client, Post("/api/v1/partien", ip, angemeldet: true))).Should().Be(HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task Magic_Link_ist_je_IP_begrenzt()
    {
        var client = await ClientAsync(new() { ["AnmeldungenJeIpUnd10Minuten"] = "1" });

        (await StatusAsync(client, Post("/api/v1/auth/anmeldung", "127.0.0.1"))).Should().Be(HttpStatusCode.Accepted);
        (await StatusAsync(client, Post("/api/v1/auth/anmeldung", "127.0.0.1"))).Should().Be(HttpStatusCode.TooManyRequests);
        AbgelehntePolicies.Should().Equal(SchachRateLimits.MagicLinkJeIp);
    }

    [Fact]
    public async Task Gleichzeitige_Tipps_sind_global_begrenzt_mit_Retry_After_2()
    {
        var client = await ClientAsync(new() { ["StockfishTippsGleichzeitig"] = "1", ["StockfishTippsWarteschlange"] = "0" });

        var laufend = client.SendAsync(Post($"/api/v1/partien/{Guid.NewGuid()}/tipp"), Ct);
        await _tippErreicht.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        using var abgelehnt = await client.SendAsync(Post($"/api/v1/partien/{Guid.NewGuid()}/tipp"), Ct);
        _tippGate.SetResult();
        using var erster = await laufend;

        abgelehnt.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        abgelehnt.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(2));
        erster.StatusCode.Should().Be(HttpStatusCode.OK);
        AbgelehntePolicies.Should().Equal(SchachRateLimits.StockfishTippsGleichzeitig);
    }

    [Fact]
    public async Task Gleichzeitige_Zuege_sind_global_begrenzt()
    {
        _zuegeBlockieren = true;
        var client = await ClientAsync(new() { ["EngineZuegeGleichzeitig"] = "1", ["EngineZuegeWarteschlange"] = "0" });

        var laufend = client.SendAsync(Post($"/api/v1/partien/{Guid.NewGuid()}/zuege"), Ct);
        await _tippErreicht.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        using var abgelehnt = await client.SendAsync(Post($"/api/v1/partien/{Guid.NewGuid()}/zuege"), Ct);
        _tippGate.SetResult();
        using var erster = await laufend;
        erster.StatusCode.Should().Be(HttpStatusCode.OK);

        abgelehnt.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        abgelehnt.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(2));
        AbgelehntePolicies.Should().Equal(SchachRateLimits.EngineZuegeGleichzeitig);
        (await StatusAsync(client, Post($"/api/v1/partien/{Guid.NewGuid()}/aufgeben"))).Should().Be(HttpStatusCode.OK, "andere Routen sind nicht betroffen");
    }

    public async ValueTask DisposeAsync()
    {
        _tippGate.TrySetResult();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        _messwerte.Dispose();
    }
}
