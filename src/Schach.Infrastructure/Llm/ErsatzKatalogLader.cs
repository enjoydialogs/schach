using System.Globalization;
using Schach.Application.Sprueche;
using Schach.Domain.Entitaeten;

namespace Schach.Infrastructure.Llm;

/// <summary>
/// Laedt <c>prompts/ersatz/{key}.de.md</c> fuer alle 8 Gegner und prueft sie (SCHACH-SPEC 6.5).
/// Wirft mit allen Befunden, wenn eine Datei fehlt oder die Pruefung nicht besteht: die App startet
/// dann nicht (<see cref="ErsatzLadepruefung"/>).
/// </summary>
public static class ErsatzKatalogLader
{
    public static ErsatzKatalog Lade(string promptsDir)
    {
        var dateien = new Dictionary<string, IReadOnlyDictionary<SpruchEreignis, IReadOnlyList<string>>>();
        var fehler = new List<string>();

        foreach (var key in ErsatzKatalog.ErwarteteGegner)
        {
            var pfad = Path.Combine(promptsDir, "ersatz", key + ".de.md");
            if (!File.Exists(pfad))
            {
                fehler.Add($"{key}: Datei fehlt ({pfad}).");
                continue;
            }

            try
            {
                var datei = ErsatzDatei.Parse(File.ReadAllText(pfad), key + ".de.md");
                fehler.AddRange(ErsatzDatei.Pruefe(datei, Name(key)).Select(f => $"{key}: {f}"));
                dateien[key] = datei;
            }
            catch (FormatException ex)
            {
                fehler.Add($"{key}: {ex.Message}");
            }
        }

        return fehler.Count == 0
            ? new ErsatzKatalog(dateien)
            : throw new InvalidOperationException("Ersatzsprueche ungueltig:" + Environment.NewLine + string.Join(Environment.NewLine, fehler));
    }

    /// <summary>Anzeigename aus dem Key (SCHACH-SPEC 4.3: Key = Vorname klein).</summary>
    public static string Name(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return char.ToUpper(key[0], CultureInfo.InvariantCulture) + key[1..];
    }
}
