using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Schach.Application.Sprueche;

/// <summary>
/// Die Daten eines Spruch-Aufrufs, in dritter Person (SCHACH-SPEC 6.3, Abschnitt 4 des Prompts).
/// Genau diese Felder, nie FEN und nie Spieler-Identitaet (kein Name, keine E-Mail, keine Elo,
/// keine Konto-ID). Material-Werte aus Sicht des Gegners.
/// </summary>
/// <param name="Laune">Laune dieses Spruchs, klein geschrieben (trocken, lustig, vorlaut, genervt).</param>
public sealed record SpruchDaten(
    string Ereignis,
    int Zugnummer,
    string Phase,
    string SpielerFarbe,
    string Bedenkzeit,
    string? SpielerZug,
    string? GegnerZug,
    bool Schach,
    int MaterialAenderung,
    int MaterialBilanz,
    string? Stellung,
    IReadOnlyList<string> GegnerZugMerkmale,
    string? Grund,
    string Laune,
    IReadOnlyList<string> LetzteSprueche)
{
    public const int MaxLetzteSprueche = 3;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Umlaute lesbar lassen: das ist ein Prompt, kein HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Kompaktes JSON fuer <c>{{daten}}</c>; <see cref="LetzteSprueche"/> auf die letzten 3 gekuerzt.</summary>
    public string AlsJson() => JsonSerializer.Serialize(
        this with { LetzteSprueche = [.. LetzteSprueche.TakeLast(MaxLetzteSprueche)] }, Json);
}
