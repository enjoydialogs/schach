using System.Text.Json;

namespace Schach.Domain.Tests;

/// <summary>Liest die gemeinsame FEN-Suite (tests/fen-suite) fuer die fachlichen Tests.</summary>
internal static class FenSuite
{
    private static readonly string SuiteDir = Path.Combine(AppContext.BaseDirectory, "fen-suite");

    public static IEnumerable<JsonElement> Faelle(string kategorie)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(SuiteDir, kategorie + ".json")));
        return doc.RootElement.GetProperty("faelle").EnumerateArray().Select(f => f.Clone()).ToList();
    }

    public static TheoryData<string> Ids(string kategorie)
    {
        var daten = new TheoryData<string>();
        foreach (var fall in Faelle(kategorie))
        {
            daten.Add(fall.GetProperty("id").GetString()!);
        }

        return daten;
    }

    public static JsonElement Fall(string kategorie, string id) =>
        Faelle(kategorie).Single(f => f.GetProperty("id").GetString() == id);

    public static string[] Zugfolge(JsonElement fall) =>
        fall.TryGetProperty("zugfolge", out var z) ? z.EnumerateArray().Select(e => e.GetString()!).ToArray() : [];
}
