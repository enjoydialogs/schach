using Schach.Application.Abstractions;

namespace Schach.Application.Partien;

/// <summary>Antwort des Gegners auf ein Remis-Angebot (SCHACH-SPEC 3.5).</summary>
public enum RemisAntwort
{
    /// <summary>Weniger als 5 Zuege nach dem letzten abgelehnten Angebot: 409 <c>RemisAngebotGesperrt</c>.</summary>
    Gesperrt,

    /// <summary><c>zugnummer &lt; 20</c>: immer abgelehnt, Grund „vor Zug 20“.</summary>
    AbgelehntVorZug20,

    /// <summary>Der Gegner sieht sich bei +1,5 oder mehr oder ein Matt: Grund „steht klar vorn“.</summary>
    AbgelehntKlarVorn,

    /// <summary>Remis durch Vereinbarung.</summary>
    Angenommen,
}

/// <summary>
/// Entscheidung ueber ein Remis-Angebot (SCHACH-SPEC 3.5, 11.2). Fuer alle Gegner gleich; nur der
/// Spruch unterscheidet sich. Die Sperren gelten vor dem Stockfish-Aufruf (<see cref="VorEngine"/>).
/// </summary>
public static class Remisangebot
{
    public const int AbZugnummer = 20;

    public const int SperreZuege = 5;

    /// <summary>Unter +1,5 aus Sicht des Gegners nimmt er an.</summary>
    public const int AnnahmeUnterCp = 150;

    /// <summary>Engine-Ausfall: annehmen bei Material bis +1 aus Sicht des Gegners (unter <c>KlarVornMaterial</c>).</summary>
    public const int AnnahmeMaterialBis = 1;

    public const string GrundVorZug20 = "vor Zug 20";

    public const string GrundKlarVorn = "steht klar vorn";

    /// <summary>Ab dieser Zugnummer ist wieder ein Angebot moeglich (Zustand <c>remisAngebotMoeglichAbZug</c>).</summary>
    public static int MoeglichAbZug(int? letztesAbgelehntBeiZug) => letztesAbgelehntBeiZug is { } z ? z + SperreZuege : 1;

    /// <summary>Sperren ohne Engine. <c>null</c> heisst: Stockfish fragen.</summary>
    public static RemisAntwort? VorEngine(int zugnummer, int? letztesAbgelehntBeiZug)
    {
        if (zugnummer < MoeglichAbZug(letztesAbgelehntBeiZug))
        {
            return RemisAntwort.Gesperrt;
        }

        return zugnummer < AbZugnummer ? RemisAntwort.AbgelehntVorZug20 : null;
    }

    /// <summary>Nach Stockfish. <paramref name="zeile"/> ist die beste Zeile von <c>/analyse</c>, <c>null</c>
    /// bei Engine-Ausfall; dann entscheidet <paramref name="materialGegnerSicht"/>.</summary>
    /// <param name="gegnerAmZug">Die Bewertung gilt fuer die Seite am Zug; ist der Spieler am Zug, dreht sie sich um.</param>
    public static RemisAntwort NachEngine(EngineAnalyseZeile? zeile, bool gegnerAmZug, int materialGegnerSicht)
    {
        if (zeile is null)
        {
            return materialGegnerSicht <= AnnahmeMaterialBis ? RemisAntwort.Angenommen : RemisAntwort.AbgelehntKlarVorn;
        }

        if (zeile.Mate is not null || zeile.Cp is not { } cp)
        {
            // „Kein Matt in Sicht“ woertlich: jedes erkannte Matt lehnt ab.
            return RemisAntwort.AbgelehntKlarVorn;
        }

        var ausGegnerSicht = gegnerAmZug ? cp : -cp;
        return ausGegnerSicht < AnnahmeUnterCp ? RemisAntwort.Angenommen : RemisAntwort.AbgelehntKlarVorn;
    }
}
