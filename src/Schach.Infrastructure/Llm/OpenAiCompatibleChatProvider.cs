using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Llm;

/// <summary>
/// Uebernahme aus Kronrat (SCHACH-SPEC 6.3, 13.3): reiner HttpClient gegen das OpenAI-kompatible
/// <c>chat/completions</c> von Mistral, EU-Endpunkt. Ein Versuch, kein Streaming, keine
/// Wiederholung; jeder Fehler wirft und der Aufrufer nimmt den Ersatzspruch. Die Klassifikation
/// aus Kronrat entfaellt.
/// </summary>
public sealed class OpenAiCompatibleChatProvider(HttpClient httpClient, IPromptCatalog prompts, LlmOptions options) : ILlmProvider
{
    private const string RequestUri = "chat/completions";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Basisadresse, Bearer-Schluessel und Timeout; genutzt von DI und Tests.</summary>
    public static HttpClient KonfiguriereClient(HttpClient client, LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        var basis = options.ChatCompletionsBase();
        if (basis.Length > 0)
        {
            client.BaseAddress = new Uri(basis);
        }

        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());
        }

        return client;
    }

    public async Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prompt = prompts.Render(request.PromptFile, request.Variables);
        var reasoning = string.IsNullOrWhiteSpace(options.ReasoningEffort) ? null : options.ReasoningEffort.Trim();
        var body = new ChatRequest(options.Model, [new ChatMessage("user", prompt)], request.MaxTokens, request.Temperature, reasoning);

        using var response = await httpClient.PostAsJsonAsync(RequestUri, body, JsonOpts, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // Kein Antworttext in der Meldung: sie landet im Log.
            throw new HttpRequestException($"LLM call failed ({(int)response.StatusCode}).", null, response.StatusCode);
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(raw);
        var (promptTokens, completionTokens) = ReadUsage(doc.RootElement);
        var content = ReadContent(doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content"));
        return new LlmCompletion(content, promptTokens, completionTokens);
    }

    private static (int PromptTokens, int CompletionTokens) ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
        {
            return (0, 0);
        }

        return (usage.TryGetProperty("prompt_tokens", out var p) && p.TryGetInt32(out var pt) ? pt : 0,
            usage.TryGetProperty("completion_tokens", out var c) && c.TryGetInt32(out var ct) ? ct : 0);
    }

    /// <summary>
    /// <c>content</c> als Zeichenkette oder als Liste typisierter Teile; nur <c>text</c>-Teile zaehlen.
    /// Eine Liste ohne Textteil wirft (das Modell hat <c>max_tokens</c> mit Denken verbraucht).
    /// </summary>
    private static string ReadContent(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Array)
        {
            return content.ValueKind == JsonValueKind.String ? content.GetString() ?? "" : "";
        }

        var text = content.EnumerateArray()
            .Where(part => part.ValueKind == JsonValueKind.Object
                           && part.TryGetProperty("type", out var type) && type.GetString() == "text"
                           && part.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
            .Select(part => part.GetProperty("text").GetString())
            .ToList();
        return text.Count == 0
            ? throw new InvalidOperationException("LLM response carried no text part.")
            : string.Concat(text);
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("reasoning_effort")] string? ReasoningEffort);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}

/// <summary>
/// Anbieter ohne Netz (Uebernahme aus Kronrat): liefert leeren Text. Der <c>SpruchPruefer</c>
/// lehnt ihn ab, also kommt der Ersatzspruch.
/// </summary>
public sealed class DeterministicFallbackProvider : ILlmProvider
{
    public Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LlmCompletion("", 0, 0));
}

/// <summary>
/// Waehlt den Anbieter. Aus, wenn <c>Llm:Provider</c> nicht „OpenAiCompatible“ ist oder Schluessel,
/// Endpunkt oder Modell fehlen: dann nur Ersatzsprueche, keine Kosten, keine Preissuche.
/// </summary>
public sealed class LlmProviderResolver(IServiceProvider serviceProvider, LlmOptions options) : ILlmProviderResolver
{
    public const string OpenAiCompatible = "OpenAiCompatible";

    public bool IsOff =>
        !string.Equals(options.Provider, OpenAiCompatible, StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(options.ApiKey)
        || options.ChatCompletionsBase().Length == 0
        || string.IsNullOrWhiteSpace(options.Model);

    public ILlmProvider Current =>
        IsOff ? Fallback : (ILlmProvider)serviceProvider.GetService(typeof(OpenAiCompatibleChatProvider))!;

    public ILlmProvider Fallback { get; } = new DeterministicFallbackProvider();

    public string ModelName => IsOff ? "" : options.Model;
}
