using System.Collections.Concurrent;

namespace Schach.Application.Partien;

/// <summary>Einstellungen, Abschnitt <c>Tipp</c> (SCHACH-SPEC 7.3, 11.2).</summary>
public sealed class TippOptions
{
    public const string SectionName = "Tipp";

    /// <summary>Stockfish-Rechenzeit je Tipp; der Engine-Dienst kappt bei 800.</summary>
    public int MovetimeMs { get; set; } = 800;

    /// <summary>Hoechstens so viele Stellungen im Speicher; danach wird der Cache geleert.</summary>
    public int CacheEintraege { get; set; } = 10_000;
}

/// <summary>Der beste Zug als Tipp (SCHACH-SPEC 7.3, Vertrag <c>TippAntwort</c>).</summary>
/// <param name="Figur">FEN-Buchstabe der ziehenden Figur, klein (k, q, r, b, n, p).</param>
public sealed record Tippzug(string Uci, string Von, string Nach, char Figur)
{
    /// <summary>Zerlegt <paramref name="uci"/> (auch mit Umwandlung, <c>e7e8q</c>) in der Stellung <paramref name="fen"/>.</summary>
    /// <exception cref="ArgumentException">Auf dem Startfeld steht keine Figur.</exception>
    public static Tippzug Aus(string fen, string uci)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uci);
        if (uci.Length is not (4 or 5))
        {
            throw new ArgumentException($"Kein UCI-Zug: '{uci}'.", nameof(uci));
        }

        var von = uci[..2];
        var figur = Brettlage.FigurAuf(fen, von) ?? throw new ArgumentException($"Auf {von} steht keine Figur.", nameof(uci));
        return new Tippzug(uci, von, uci[2..4], char.ToLowerInvariant(figur));
    }
}

/// <summary>Tipp-Ergebnis je FEN (SCHACH-SPEC 7.3). Singleton; gleiche Stellung, gleicher Tipp.</summary>
public sealed class TippCache(TippOptions options)
{
    private readonly ConcurrentDictionary<string, string> _zuege = new(StringComparer.Ordinal);

    public bool Versuche(string fen, out string uci) => _zuege.TryGetValue(fen, out uci!);

    public void Merke(string fen, string uci)
    {
        if (_zuege.Count >= Math.Max(1, options.CacheEintraege))
        {
            _zuege.Clear();
        }

        _zuege[fen] = uci;
    }
}
