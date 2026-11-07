using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Engine;

/// <summary>
/// HTTP-Client des Engine-Dienstes nach <c>engine/API.md</c> (SCHACH-SPEC 5.2, 8.10).
/// <list type="bullet">
/// <item>Aeusserer Timeout 5 s je Anfrage (<see cref="HttpClient.Timeout"/>).</item>
/// <item><c>/move</c> mit Maia: Frist 3 s je Versuch, ein Retry, dann Fairy-Stockfish. Faellt auch
/// das aus: <see cref="EngineNichtVerfuegbarException"/> (der Aufrufer meldet
/// <c>gegnerZugAusstehend</c>, der Sweep wiederholt).</item>
/// <item><c>/analyse</c>: ein Versuch. 503 oder Timeout: <see cref="EngineNichtVerfuegbarException"/>
/// (Tipp: <c>TippNichtVerfuegbar</c>, Remis-Bewertung: Materialbilanz).</item>
/// <item>400, 409 und unlesbare Antworten: <see cref="EngineFehlerException"/>, nie wiederholt.</item>
/// </list>
/// </summary>
internal sealed class EngineHttpClient : IEngineClient
{
    /// <summary>Untergrenze von Fairy-Stockfish <c>UCI_Elo</c> (SCHACH-SPEC 5.1).</summary>
    internal const int FairyMindestElo = 500;

    /// <summary>Name des HttpClients in der <c>IHttpClientFactory</c>.</summary>
    internal const string HttpClientName = "engine";

    private static readonly Uri MovePfad = new("/move", UriKind.Relative);
    private static readonly Uri AnalysePfad = new("/analyse", UriKind.Relative);
    private static readonly Uri HealthzPfad = new("/healthz", UriKind.Relative);

    private readonly HttpClient _http;
    private readonly EngineClientOptions _options;
    private readonly SchachZaehler _zaehler;
    private readonly EngineUeberlastWaechter _waechter;

    public EngineHttpClient(HttpClient http, IOptions<EngineClientOptions> options, SchachZaehler zaehler, EngineUeberlastWaechter waechter)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options.Value;
        _zaehler = zaehler;
        _waechter = waechter;
    }

    public async Task<EngineMoveResponse> MoveAsync(EngineMoveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Engine == EngineArt.Fairy)
        {
            return await PostAsync<EngineMoveResponse>(MovePfad, request, frist: null, cancellationToken);
        }

        for (var versuch = 1; versuch <= 2; versuch++)
        {
            try
            {
                return await PostAsync<EngineMoveResponse>(MovePfad, request, _options.MaiaTimeout, cancellationToken);
            }
            catch (EngineNichtVerfuegbarException)
            {
                // Naechster Versuch, danach Fairy-Stockfish.
            }
        }

        FehlerGemeldet(EngineFehlerArt.FairyErsatz);
        var ersatz = request with
        {
            Engine = EngineArt.Fairy,
            FairySkill = request.FairySkill ?? Math.Max(FairyMindestElo, request.SelfElo),
        };
        return await PostAsync<EngineMoveResponse>(MovePfad, ersatz, frist: null, cancellationToken);
    }

    public Task<EngineAnalyseResponse> AnalyseAsync(EngineAnalyseRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PostAsync<EngineAnalyseResponse>(AnalysePfad, request, frist: null, cancellationToken);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        BaseUrlPruefen();
        try
        {
            using var response = await _http.GetAsync(HealthzPfad, cancellationToken);
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch (Exception e) when (e is HttpRequestException || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return false;
        }
    }

    private void FehlerGemeldet(EngineFehlerArt art)
    {
        _zaehler.EngineFehler(art.ToString());
        if (art is EngineFehlerArt.Zeitueberschreitung or EngineFehlerArt.Ueberlastet or EngineFehlerArt.Verbindung)
        {
            _waechter.AusfallMelden();
        }
    }

    private void BaseUrlPruefen()
    {
        if (_http.BaseAddress is null)
        {
            throw new InvalidOperationException("Engine:BaseUrl fehlt (Compose setzt Engine__BaseUrl).");
        }
    }

    private async Task<T> PostAsync<T>(Uri pfad, object body, TimeSpan? frist, CancellationToken cancellationToken)
        where T : class
    {
        BaseUrlPruefen();
        using var versuch = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (frist is { } f)
        {
            versuch.CancelAfter(f);
        }

        try
        {
            using var response = await _http.PostAsJsonAsync(pfad, body, versuch.Token);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw NichtVerfuegbar(EngineFehlerArt.Ueberlastet, $"{pfad} 503 {await FehlercodeAsync(response, versuch.Token)}");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw Vertrag($"{pfad} {(int)response.StatusCode} {await FehlercodeAsync(response, versuch.Token)}");
            }

            T? inhalt;
            try
            {
                inhalt = await response.Content.ReadFromJsonAsync<T>(versuch.Token);
            }
            catch (JsonException e)
            {
                throw Vertrag($"{pfad} unlesbare Antwort", e);
            }

            return inhalt ?? throw Vertrag($"{pfad} leere Antwort");
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        {
            // Versuchsfrist oder aeusserer HttpClient-Timeout, nicht der Aufrufer.
            throw NichtVerfuegbar(EngineFehlerArt.Zeitueberschreitung, $"{pfad} Zeitueberschreitung", e);
        }
        catch (HttpRequestException e)
        {
            throw NichtVerfuegbar(EngineFehlerArt.Verbindung, $"{pfad} keine Verbindung", e);
        }
    }

    private EngineNichtVerfuegbarException NichtVerfuegbar(EngineFehlerArt art, string meldung, Exception? inner = null)
    {
        FehlerGemeldet(art);
        return inner is null ? new EngineNichtVerfuegbarException(meldung) : new EngineNichtVerfuegbarException(meldung, inner);
    }

    private EngineFehlerException Vertrag(string meldung, Exception? inner = null)
    {
        FehlerGemeldet(EngineFehlerArt.Vertrag);
        return inner is null ? new EngineFehlerException(meldung) : new EngineFehlerException(meldung, inner);
    }

    /// <summary>Nur der <c>error</c>-Code aus <c>{error, detail}</c>; <c>detail</c> bleibt draussen.</summary>
    private static async Task<string> FehlercodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var fehler = await response.Content.ReadFromJsonAsync<EngineFehlerAntwort>(cancellationToken);
            return fehler?.Error ?? "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private sealed record EngineFehlerAntwort(
        [property: System.Text.Json.Serialization.JsonPropertyName("error")] string? Error);
}

/// <summary>Wert des Tags <c>art</c> an <c>schach.engine.fehler</c> (SCHACH-SPEC 14.2).</summary>
public enum EngineFehlerArt
{
    /// <summary>Versuchsfrist oder aeusserer Timeout.</summary>
    Zeitueberschreitung,

    /// <summary>503 des Dienstes (Warteschlange, Pool, Rolle, Start).</summary>
    Ueberlastet,

    /// <summary>Keine Verbindung.</summary>
    Verbindung,

    /// <summary>400, 409, 500 oder unlesbare Antwort: Programmfehler.</summary>
    Vertrag,

    /// <summary>Maia fiel zweimal aus, Fairy-Stockfish springt ein.</summary>
    FairyErsatz,
}
