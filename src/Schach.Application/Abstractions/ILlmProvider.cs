namespace Schach.Application.Abstractions;

/// <summary>
/// Sprachmodell fuer Sprueche (SCHACH-SPEC 6.3). Umsetzungen: <c>OpenAiCompatibleChatProvider</c>
/// (Mistral, EU-Endpunkt) und <c>DeterministicFallbackProvider</c>, gewaehlt von
/// <see cref="ILlmProviderResolver"/>. Ein Versuch, kein Streaming; Zeitlimit setzt der Aufrufer
/// ueber das <see cref="CancellationToken"/>.
/// </summary>
public interface ILlmProvider
{
    Task<LlmCompletion> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);
}

/// <param name="PromptFile">Pfad unter <c>prompts/</c>, z. B. <c>spruch.de.md</c>.</param>
/// <param name="Variables">Platzhalter des Prompts, z. B. <c>name</c>, <c>persona</c>, <c>daten</c>.</param>
public sealed record LlmRequest(
    string PromptFile,
    IReadOnlyDictionary<string, string> Variables,
    int MaxTokens,
    double Temperature);

/// <param name="Text">Nur die <c>text</c>-Teile der Antwort, zusammengefuegt.</param>
public sealed record LlmCompletion(string Text, int PromptTokens, int CompletionTokens);

/// <summary>Waehlt den aktiven Anbieter (<c>Llm:Provider</c>) und haelt den Fallback bereit.</summary>
public interface ILlmProviderResolver
{
    ILlmProvider Current { get; }

    ILlmProvider Fallback { get; }

    /// <summary>Wahr, wenn kein echter Anbieter konfiguriert ist. Dann kommen nur Ersatzsprueche.</summary>
    bool IsOff { get; }

    /// <summary>Konfigurierter Modellname (<c>Llm:Model</c>), leer wenn <see cref="IsOff"/>.</summary>
    string ModelName { get; }
}
