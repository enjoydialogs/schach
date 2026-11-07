using Chess;

namespace Schach.Domain;

/// <summary>
/// Unveraenderliche Stellung auf Basis von Gera.Chess (SCHACH-SPEC 3.4.9): Gera.Chess liefert nur
/// Legalitaet, Matt und Patt. Alle Remisregeln entscheidet <see cref="Remisregeln"/>.
/// Zuege sind UCI-Strings (<c>e2e4</c>, <c>e1g1</c>, <c>a7a8q</c>).
/// </summary>
public sealed class Stellung
{
    private readonly ChessBoard _brett;

    private Stellung(ChessBoard brett, string fen)
    {
        _brett = brett;
        Fen = fen;
    }

    /// <summary>Vollstaendige FEN mit sechs Feldern.</summary>
    public string Fen { get; }

    /// <summary>Laedt eine Stellung aus einer FEN.</summary>
    /// <exception cref="ArgumentException">FEN ist nicht lesbar.</exception>
    public static Stellung AusFen(string fen)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        if (!ChessBoard.TryLoadFromFen(fen, out var brett, AutoEndgameRules.None) || brett is null)
        {
            throw new ArgumentException($"FEN nicht lesbar: {fen}", nameof(fen));
        }

        return new Stellung(brett, fen);
    }

    /// <summary>Seite am Zug.</summary>
    public Farbe AmZug => _brett.Turn == PieceColor.White ? Farbe.Weiss : Farbe.Schwarz;

    /// <summary>Der Koenig der Seite am Zug steht im Schach.</summary>
    public bool IstSchach => AmZug == Farbe.Weiss ? _brett.WhiteKingChecked : _brett.BlackKingChecked;

    /// <summary>Die Seite am Zug ist matt.</summary>
    public bool IstMatt => IstSchach && GeraZuege().Length == 0;

    /// <summary>Die Seite am Zug hat keinen erlaubten Zug und steht nicht im Schach.</summary>
    public bool IstPatt => !IstSchach && GeraZuege().Length == 0;

    /// <summary>In der Stellung ist ein Schlag en passant erlaubt.</summary>
    public bool HatEnPassantSchlag => GeraZuege().Any(z => z.IsEnPassant);

    /// <summary>Alle erlaubten Zuege als UCI, sortiert (ordinal).</summary>
    public IReadOnlyList<string> ErlaubteZuege() =>
        GeraZuege().Select(Uci).Order(StringComparer.Ordinal).ToList();

    /// <summary>Spielt UCI-Zuege nacheinander und liefert die Stellung danach.</summary>
    /// <exception cref="ZugUnzulaessigException">Ein Zug ist in seiner Stellung nicht erlaubt.</exception>
    public Stellung Ziehe(params IEnumerable<string> uciZuege)
    {
        ArgumentNullException.ThrowIfNull(uciZuege);
        var stellung = this;
        foreach (var uci in uciZuege)
        {
            var zug = stellung.GeraZuege().FirstOrDefault(z => Uci(z) == uci)
                ?? throw new ZugUnzulaessigException(uci, stellung.Fen);
            stellung = stellung.Nach(zug);
        }

        return stellung;
    }

    /// <summary>SAN eines erlaubten UCI-Zugs in dieser Stellung, mit <c>+</c> oder <c>#</c> (fuer PGN).</summary>
    /// <exception cref="ZugUnzulaessigException">Der Zug ist nicht erlaubt.</exception>
    public string San(string uci)
    {
        var zug = GeraZuege().FirstOrDefault(z => Uci(z) == uci)
            ?? throw new ZugUnzulaessigException(uci, Fen);
        return _brett.ParseToSan(zug);
    }

    /// <summary>Anzahl der Blattknoten bis zur gegebenen Tiefe (Perft).</summary>
    public long Perft(int tiefe)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tiefe);
        if (tiefe == 0)
        {
            return 1;
        }

        var zuege = GeraZuege();
        if (tiefe == 1)
        {
            return zuege.Length;
        }

        long summe = 0;
        foreach (var zug in zuege)
        {
            summe += Nach(zug).Perft(tiefe - 1);
        }

        return summe;
    }

    private Move[] GeraZuege() =>
        _brett.IsEndGame ? [] : _brett.Moves(allowAmbiguousCastle: false, generateSan: false);

    private static string Uci(Move zug)
    {
        var uci = zug.OriginalPosition.ToString() + zug.NewPosition.ToString();
        return zug.Promotion is { } figur ? uci + char.ToLowerInvariant(figur.Type.AsChar) : uci;
    }

    private Stellung Nach(Move zug)
    {
        var brett = ChessBoard.LoadFromFen(Fen, AutoEndgameRules.None);
        brett.Move(zug);
        var fen = brett.ToFen();
        return new Stellung(brett, fen);
    }
}
