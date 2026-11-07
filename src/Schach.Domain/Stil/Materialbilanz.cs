namespace Schach.Domain.Stil;

/// <summary>Materialbilanz für die Remis-Vermeidung (Spec 5.3 Schritt 4a, 3.5): D 9, T 5, L 3, S 3, B 1.</summary>
public static class Materialbilanz
{
    /// <summary>Eigenes minus gegnerisches Material aus Sicht der Seite am Zug (FEN-Feld 2).</summary>
    public static int FuerSeiteAmZug(string fen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        var teile = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var weissAmZug = teile.Length < 2 || teile[1] == "w";
        var bilanzWeiss = 0;
        foreach (var c in teile[0])
        {
            var wert = char.ToLowerInvariant(c) switch
            {
                'q' => 9,
                'r' => 5,
                'b' or 'n' => 3,
                'p' => 1,
                _ => 0,
            };
            bilanzWeiss += char.IsAsciiLetterUpper(c) ? wert : -wert;
        }

        return weissAmZug ? bilanzWeiss : -bilanzWeiss;
    }
}
