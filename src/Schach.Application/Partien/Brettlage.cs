using Schach.Domain;

namespace Schach.Application.Partien;

/// <summary>Kleine FEN-Helfer fuer Tipp, Remis und Sprueche. Rein, ohne Gera.Chess.</summary>
public static class Brettlage
{
    /// <summary>Zugnummer aus FEN-Feld 6 (SCHACH-SPEC 3.5); 1, wenn das Feld fehlt.</summary>
    public static int Zugnummer(string fen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        var teile = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return teile.Length >= 6 && int.TryParse(teile[5], System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 1;
    }

    /// <summary>Figur auf <paramref name="feld"/> (z. B. <c>e4</c>) als FEN-Buchstabe, sonst <c>null</c>.</summary>
    public static char? FigurAuf(string fen, string feld)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        ArgumentException.ThrowIfNullOrWhiteSpace(feld);
        if (feld.Length != 2 || feld[0] is < 'a' or > 'h' || feld[1] is < '1' or > '8')
        {
            throw new ArgumentException($"Kein Feld: '{feld}'.", nameof(feld));
        }

        var reihen = fen.Split(' ')[0].Split('/');
        var reihe = reihen['8' - feld[1]];
        var spalte = 0;
        foreach (var c in reihe)
        {
            if (char.IsAsciiDigit(c))
            {
                spalte += c - '0';
                continue;
            }

            if (spalte == feld[0] - 'a')
            {
                return c;
            }

            spalte++;
        }

        return null;
    }

    /// <summary>Material D 9, T 5, L 3, S 3, B 1 aus Sicht von <paramref name="seite"/>.</summary>
    public static int Material(string fen, Farbe seite)
    {
        var amZug = Domain.Stil.Materialbilanz.FuerSeiteAmZug(fen);
        var weissAmZug = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } t ? t[1] == "w" : true;
        return (seite == Farbe.Weiss) == weissAmZug ? amZug : -amZug;
    }

    /// <summary>Summe der Figuren ausser Bauern und Koenigen beider Seiten (Start: 62).</summary>
    public static int Figurenmaterial(string fen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        return fen.Split(' ')[0].Sum(c => char.ToLowerInvariant(c) switch
        {
            'q' => 9,
            'r' => 5,
            'b' or 'n' => 3,
            _ => 0,
        });
    }

    /// <summary>Partiephase fuer den Spruch-Prompt: bis Zug 10 Eroeffnung, ab 26 Punkten Figurenmaterial
    /// oder weniger Endspiel, sonst Mittelspiel.</summary>
    public static string Phase(string fen) =>
        Zugnummer(fen) <= 10 ? "Eröffnung" : Figurenmaterial(fen) <= 26 ? "Endspiel" : "Mittelspiel";

    public static Farbe Andere(Farbe farbe) => farbe == Farbe.Weiss ? Farbe.Schwarz : Farbe.Weiss;
}
