using System.Text.Json.Serialization;

namespace Schach.Application.Abstractions;

// Draht-DTOs des Engine-Dienstes. Spiegeln engine/API.md Feld fuer Feld (eingefroren ab Phase 0).
// Der Engine-Dienst spricht snake_case, die Schach-API camelCase: darum traegt hier jedes Feld
// einen expliziten JsonPropertyName und haengt nicht an einer Naming Policy.

/// <summary>Welche Engine <c>POST /move</c> rechnet.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EngineArt>))]
public enum EngineArt
{
    [JsonStringEnumMemberName("maia")]
    Maia,

    [JsonStringEnumMemberName("fairy")]
    Fairy,
}

/// <summary>Anfrage an <c>POST /move</c>.</summary>
/// <param name="Fen">Ausgangsstellung vor <paramref name="Moves"/> (in der Regel die Grundstellung).</param>
/// <param name="Moves">Alle Halbzuege seit <paramref name="Fen"/> in UCI. Gerechnet wird fuer die Stellung danach.</param>
/// <param name="SelfElo">Geeichte Elo der Gegner-Spielstaerke (SCHACH-SPEC 5.3 Schritt 1).</param>
/// <param name="OppoElo">Elo des Spielers, Gast 800.</param>
/// <param name="FairySkill">Nur bei <see cref="EngineArt.Fairy"/>: <c>UCI_Elo</c> ab 500.</param>
public sealed record EngineMoveRequest(
    [property: JsonPropertyName("fen")] string Fen,
    [property: JsonPropertyName("moves")] IReadOnlyList<string> Moves,
    [property: JsonPropertyName("self_elo")] int SelfElo,
    [property: JsonPropertyName("oppo_elo")] int OppoElo,
    [property: JsonPropertyName("engine")] EngineArt Engine,
    [property: JsonPropertyName("fairy_skill")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? FairySkill = null);

/// <summary>Antwort von <c>POST /move</c>. Maia fuellt <see cref="Policy"/> und <see cref="Wdl"/>,
/// Fairy-Stockfish nur <see cref="Bestmove"/>.</summary>
public sealed record EngineMoveResponse(
    [property: JsonPropertyName("engine")] EngineArt Engine,
    [property: JsonPropertyName("policy")] IReadOnlyList<EnginePolicyEintrag>? Policy,
    [property: JsonPropertyName("wdl")] EngineWdl? Wdl,
    [property: JsonPropertyName("bestmove")] string? Bestmove);

/// <summary>Ein erlaubter Zug mit Maia-Wahrscheinlichkeit <c>p</c> (Summe ueber alle Eintraege = 1).</summary>
public sealed record EnginePolicyEintrag(
    [property: JsonPropertyName("uci")] string Uci,
    [property: JsonPropertyName("p")] double P);

/// <summary>Gewinn, Remis, Verlust als Wahrscheinlichkeiten in [0, 1], Summe 1, immer aus Sicht der
/// Seite am Zug in der bewerteten Stellung (also bei <c>/move</c> aus Sicht des Gegners).</summary>
public sealed record EngineWdl(
    [property: JsonPropertyName("w")] double W,
    [property: JsonPropertyName("d")] double D,
    [property: JsonPropertyName("l")] double L);

/// <summary>Anfrage an <c>POST /analyse</c> (Stockfish voll).</summary>
/// <param name="MovetimeMs">Hoechstens 800 (<c>SF_MAX_MOVETIME_MS</c>); groessere Werte kappt der Dienst.</param>
/// <param name="Multipv">1 bis 3.</param>
public sealed record EngineAnalyseRequest(
    [property: JsonPropertyName("fen")] string Fen,
    [property: JsonPropertyName("moves")] IReadOnlyList<string> Moves,
    [property: JsonPropertyName("movetime_ms")] int MovetimeMs,
    [property: JsonPropertyName("multipv")] int Multipv);

/// <summary>Antwort von <c>POST /analyse</c>. <see cref="Lines"/> ist nach Rang sortiert, Zeile 1 = <see cref="Bestmove"/>.</summary>
public sealed record EngineAnalyseResponse(
    [property: JsonPropertyName("bestmove")] string Bestmove,
    [property: JsonPropertyName("lines")] IReadOnlyList<EngineAnalyseZeile> Lines);

/// <summary>Eine Hauptvariante. <see cref="Cp"/> in Centipawns aus Sicht der Seite am Zug;
/// bei erkanntem Matt ist <see cref="Cp"/> <c>null</c> und <see cref="Mate"/> die Zahl der Zuege
/// (positiv: Seite am Zug setzt matt, negativ: wird matt gesetzt).</summary>
public sealed record EngineAnalyseZeile(
    [property: JsonPropertyName("uci")] string Uci,
    [property: JsonPropertyName("cp")] int? Cp,
    [property: JsonPropertyName("mate")] int? Mate,
    [property: JsonPropertyName("wdl")] EngineWdl Wdl);
