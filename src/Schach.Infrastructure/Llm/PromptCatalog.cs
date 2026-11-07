using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Schach.Infrastructure.Llm;

/// <summary>Laedt Prompt-Dateien unter <c>prompts/</c> und fuellt ihre Platzhalter.</summary>
public interface IPromptCatalog
{
    /// <summary>
    /// Laedt <paramref name="promptFile"/> (relativ zu <c>prompts/</c>) und ersetzt die
    /// <c>{{variable}}</c>-Platzhalter. Wirft, wenn die Datei fehlt oder ein Platzhalter keinen Wert hat.
    /// </summary>
    string Render(string promptFile, IReadOnlyDictionary<string, string> variables);
}

/// <summary>
/// Uebernahme aus Kronrat (SCHACH-SPEC 13.3). Ersetzt in EINEM Durchlauf ueber die Vorlage: ein
/// eingesetzter Wert, der selbst wie ein Platzhalter aussieht, wird nicht noch einmal ersetzt
/// (Injektionshygiene, die Daten kommen aus der Partie). Dateien werden einmal gelesen und gecacht.
/// </summary>
public sealed partial class PromptCatalog(string promptsDir) : IPromptCatalog
{
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public string Render(string promptFile, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        var template = _cache.GetOrAdd(promptFile, file => File.ReadAllText(Path.Combine(promptsDir, file)));

        string? missing = null;
        var result = PlaceholderPattern().Replace(template, match =>
        {
            var key = match.Value[2..^2];
            if (variables.TryGetValue(key, out var value))
            {
                return value;
            }

            missing ??= match.Value;
            return match.Value;
        });

        return missing is null
            ? result
            : throw new InvalidOperationException($"Unresolved placeholder {missing} in prompt file '{promptFile}'.");
    }

    [GeneratedRegex(@"\{\{[a-zA-Z0-9_]+\}\}")]
    private static partial Regex PlaceholderPattern();
}
