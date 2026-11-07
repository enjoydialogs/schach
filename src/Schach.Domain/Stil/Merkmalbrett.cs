using System.Globalization;

namespace Schach.Domain.Stil;

/// <summary>Ein Feld, Linie 0 bis 7 (a bis h), Reihe 0 bis 7 (1 bis 8).</summary>
internal readonly record struct Feld(int Linie, int Reihe)
{
    public string Name => string.Create(CultureInfo.InvariantCulture, $"{(char)('a' + Linie)}{Reihe + 1}");

    public bool AufBrett => Linie is >= 0 and < 8 && Reihe is >= 0 and < 8;

    public static Feld Parse(ReadOnlySpan<char> name) => new(name[0] - 'a', name[1] - '1');

    /// <summary>Königsabstand (Chebyshev).</summary>
    public static int Abstand(Feld a, Feld b) => Math.Max(Math.Abs(a.Linie - b.Linie), Math.Abs(a.Reihe - b.Reihe));
}

internal readonly record struct UciZug(Feld Von, Feld Nach, char? Umwandlung);

/// <summary>
/// Schlankes Brett nur für die Zugmerkmale: Figurenstellung aus der FEN, Zug ausführen (inkl.
/// Rochade, en passant, Umwandlung) und geometrische Angriffe. Prüft keine Legalität; das macht
/// vorher Gera.Chess. Figuren als FEN-Zeichen (groß = Weiß), leeres Feld = ' '.
/// </summary>
internal sealed class Merkmalbrett
{
    private readonly char[,] _felder;

    private Merkmalbrett(char[,] felder, bool weissAmZug, string enPassant)
    {
        _felder = felder;
        WeissAmZug = weissAmZug;
        EnPassant = enPassant;
    }

    public bool WeissAmZug { get; }

    public string EnPassant { get; }

    public char this[Feld f] => _felder[f.Linie, f.Reihe];

    public static Merkmalbrett AusFen(string fen)
    {
        var teile = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var felder = new char[8, 8];
        var reihen = teile[0].Split('/');
        for (var r = 0; r < 8; r++)
        {
            var linie = 0;
            foreach (var c in reihen[r])
            {
                if (char.IsAsciiDigit(c))
                {
                    for (var i = 0; i < c - '0'; i++)
                    {
                        felder[linie++, 7 - r] = ' ';
                    }
                }
                else
                {
                    felder[linie++, 7 - r] = c;
                }
            }
        }

        return new Merkmalbrett(felder, teile.Length < 2 || teile[1] == "w", teile.Length > 3 ? teile[3] : "-");
    }

    /// <summary>Figurenwert: B 1, S 3, L 3, T 5, D 9, K sehr hoch.</summary>
    public static int Wert(char figur) => char.ToLowerInvariant(figur) switch
    {
        'p' => 1,
        'n' or 'b' => 3,
        'r' => 5,
        'q' => 9,
        'k' => 100,
        _ => 0,
    };

    public static bool IstWeiss(char figur) => char.IsAsciiLetterUpper(figur);

    public static UciZug Zug(string uci) =>
        new(Feld.Parse(uci.AsSpan(0, 2)), Feld.Parse(uci.AsSpan(2, 2)), uci.Length == 5 ? uci[4] : null);

    public bool IstEnPassant(UciZug zug) =>
        char.ToLowerInvariant(this[zug.Von]) == 'p' && zug.Von.Linie != zug.Nach.Linie && this[zug.Nach] == ' ';

    public bool IstRochade(UciZug zug) =>
        char.ToLowerInvariant(this[zug.Von]) == 'k'
        && (Math.Abs(zug.Nach.Linie - zug.Von.Linie) == 2 || IstEigeneFigur(zug.Nach));

    public bool IstSchlag(UciZug zug) => IstEnPassant(zug) || (!IstRochade(zug) && this[zug.Nach] != ' ');

    public char GeschlageneFigur(UciZug zug) =>
        IstEnPassant(zug) ? this[new Feld(zug.Nach.Linie, zug.Von.Reihe)] : this[zug.Nach];

    /// <summary>Stellung nach dem Zug; danach ist die andere Seite am Zug.</summary>
    public Merkmalbrett Nach(UciZug zug)
    {
        var f = (char[,])_felder.Clone();
        var figur = this[zug.Von];
        if (IstRochade(zug))
        {
            // UCI e1g1/e1c1; Gera-Schreibweise e1h1/e1a1 (König auf Turm) wird mit abgebildet.
            var kurz = zug.Nach.Linie > zug.Von.Linie;
            var reihe = zug.Von.Reihe;
            f[zug.Von.Linie, reihe] = ' ';
            f[kurz ? 7 : 0, reihe] = ' ';
            f[kurz ? 6 : 2, reihe] = figur;
            f[kurz ? 5 : 3, reihe] = IstWeiss(figur) ? 'R' : 'r';
            return new Merkmalbrett(f, !WeissAmZug, "-");
        }

        if (IstEnPassant(zug))
        {
            f[zug.Nach.Linie, zug.Von.Reihe] = ' ';
        }

        f[zug.Von.Linie, zug.Von.Reihe] = ' ';
        f[zug.Nach.Linie, zug.Nach.Reihe] = zug.Umwandlung is { } u
            ? (IstWeiss(figur) ? char.ToUpperInvariant(u) : char.ToLowerInvariant(u))
            : figur;
        return new Merkmalbrett(f, !WeissAmZug, "-");
    }

    public Feld Koenig(bool weiss)
    {
        var gesucht = weiss ? 'K' : 'k';
        for (var l = 0; l < 8; l++)
        {
            for (var r = 0; r < 8; r++)
            {
                if (_felder[l, r] == gesucht)
                {
                    return new Feld(l, r);
                }
            }
        }

        throw new InvalidOperationException("Kein König auf dem Brett.");
    }

    /// <summary>Figuren (FEN-Zeichen) der Seite <paramref name="durchWeiss"/>, die das Feld geometrisch angreifen.</summary>
    public List<char> Angreifer(Feld ziel, bool durchWeiss)
    {
        var treffer = new List<char>();

        void Pruefe(Feld f, string typen)
        {
            if (f.AufBrett && this[f] != ' ' && IstWeiss(this[f]) == durchWeiss && typen.Contains(char.ToLowerInvariant(this[f]), StringComparison.Ordinal))
            {
                treffer.Add(this[f]);
            }
        }

        // Bauer: Weiß greift nach oben an, also steht ein weißer Angreifer eine Reihe tiefer.
        var bauernReihe = durchWeiss ? -1 : 1;
        Pruefe(new Feld(ziel.Linie - 1, ziel.Reihe + bauernReihe), "p");
        Pruefe(new Feld(ziel.Linie + 1, ziel.Reihe + bauernReihe), "p");

        foreach (var (dl, dr) in Springer)
        {
            Pruefe(new Feld(ziel.Linie + dl, ziel.Reihe + dr), "n");
        }

        foreach (var (dl, dr) in Koenigsschritte)
        {
            Pruefe(new Feld(ziel.Linie + dl, ziel.Reihe + dr), "k");
        }

        foreach (var (dl, dr) in Koenigsschritte)
        {
            var typen = dl == 0 || dr == 0 ? "rq" : "bq";
            var f = new Feld(ziel.Linie + dl, ziel.Reihe + dr);
            while (f.AufBrett && this[f] == ' ')
            {
                f = new Feld(f.Linie + dl, f.Reihe + dr);
            }

            Pruefe(f, typen);
        }

        return treffer;
    }

    private bool IstEigeneFigur(Feld f) => this[f] != ' ' && IstWeiss(this[f]) == WeissAmZug;

    private static readonly (int, int)[] Springer = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];

    private static readonly (int, int)[] Koenigsschritte = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];
}
