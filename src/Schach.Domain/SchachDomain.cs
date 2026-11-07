namespace Schach.Domain;

/// <summary>Anker der Domain-Assembly. Phase 0a legt nur das Projekt an; die Fachklassen
/// (Partie, Zugfolge, Uhr, Elo, <c>Remisregeln</c>, Gegner-Katalog, Zugmerkmale) folgen ab S1a/S1b.</summary>
public static class SchachDomain
{
    /// <summary>FEN der Grundstellung.</summary>
    public const string GrundstellungFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
}
