using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Schach.Application.Sprachausgabe;
using Schach.Infrastructure.Llm;

namespace Schach.Infrastructure.Sprachausgabe;

/// <summary>
/// Mistral Voxtral TTS (SCHACH-SPEC 6.9): <c>POST {Endpoint}/audio/speech</c> mit <c>model</c>,
/// <c>input</c>, <c>voice_id</c> und <c>response_format=mp3</c>. Die Antwort ist JSON mit
/// <c>audio_data</c> (Base64). Ein Versuch, kein Streaming; jeder Fehler wirft.
/// Schluessel und Schalter kommen aus <c>Llm</c>: ohne Schluessel oder mit <c>Llm:Provider</c> aus
/// gibt es keine Stimme.
/// </summary>
public sealed class MistralSprachProvider(IHttpClientFactory clients, LlmOptions llm, SprachausgabeOptions options) : ISprachProvider
{
    /// <summary>Name des HttpClients; je Aufruf neu aus der Factory, damit der Handler rotiert.</summary>
    public const string ClientName = "Sprachausgabe";

    private const string RequestUri = "audio/speech";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool Verfuegbar =>
        string.Equals(llm.Provider, LlmProviderResolver.OpenAiCompatible, StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(llm.ApiKey)
        && Basis(options).Length > 0;

    /// <summary>Basisadresse, Bearer-Schluessel und Timeout (ueber dem Zeitlimit des Dienstes).</summary>
    public static HttpClient KonfiguriereClient(HttpClient client, LlmOptions llm, SprachausgabeOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(options);
        var basis = Basis(options);
        if (basis.Length > 0)
        {
            client.BaseAddress = new Uri(basis);
        }

        client.Timeout = TimeSpan.FromMilliseconds(options.ZeitlimitMs + 2000);
        if (!string.IsNullOrWhiteSpace(llm.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", llm.ApiKey.Trim());
        }

        return client;
    }

    public async Task<byte[]> SprichAsync(SprachAnfrage anfrage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        var body = new SpeechRequest(anfrage.Model, anfrage.Text, anfrage.VoiceId, "mp3", anfrage.Expressiveness);
        using var httpClient = clients.CreateClient(ClientName);
        using var response = await httpClient.PostAsJsonAsync(RequestUri, body, JsonOpts, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Kein Antworttext in der Meldung: sie landet im Log.
            throw new HttpRequestException($"TTS call failed ({(int)response.StatusCode}).", null, response.StatusCode);
        }

        var antwort = await response.Content.ReadFromJsonAsync<SpeechResponse>(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(antwort?.AudioData)
            ? throw new InvalidOperationException("TTS response carried no audio_data.")
            : Convert.FromBase64String(antwort.AudioData);
    }

    private static string Basis(SprachausgabeOptions options)
    {
        var trimmed = options.Endpoint.Trim().TrimEnd('/');
        return trimmed.Length == 0 ? "" : trimmed + "/";
    }

    private sealed record SpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("voice_id")] string VoiceId,
        [property: JsonPropertyName("response_format")] string ResponseFormat,
        [property: JsonPropertyName("expressiveness")] double? Expressiveness);

    private sealed record SpeechResponse([property: JsonPropertyName("audio_data")] string? AudioData);
}

/// <summary>Stimm-Token per Data Protection (Schluesselring in Postgres, SCHACH-SPEC 9.1). Base64url.</summary>
public sealed class StimmSiegel(IDataProtectionProvider provider) : IStimmSiegel
{
    public const string Zweck = "Schach.Sprachausgabe.Stimme.v1";

    private readonly IDataProtector _schutz = provider.CreateProtector(Zweck);

    public string Versiegeln(string inhalt) => _schutz.Protect(inhalt);

    public string? Oeffnen(string token)
    {
        try
        {
            return _schutz.Unprotect(token);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
