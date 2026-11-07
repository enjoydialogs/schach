using System.Text.Json;
using System.Text.Json.Serialization;

namespace Schach.Application.Llm;

/// <summary>
/// Preis eines Modells in ganzen Cent je Million Token, getrennt nach Ein- und Ausgabe
/// (Uebernahme aus Kronrat). Werte aus der Preisseite des Anbieters abschreiben, nicht umrechnen.
/// </summary>
public sealed record ModelPrice(
    [property: JsonPropertyName("in")] int InputCentsPerMillion,
    [property: JsonPropertyName("out")] int OutputCentsPerMillion);

/// <summary>Liest <c>Llm:ModelPricesCentsPerMillion</c>, z. B. <c>{"ministral-8b-2512":{"in":17,"out":17}}</c>.</summary>
public static class ModelPrices
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary><c>null</c> heisst „nicht hinterlegt“: der Aufrufer behandelt das als erschoepftes Budget.</summary>
    public static ModelPrice? For(IReadOnlyDictionary<string, ModelPrice> prices, string modelName)
    {
        ArgumentNullException.ThrowIfNull(prices);
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        foreach (var (key, value) in prices)
        {
            if (string.Equals(key, modelName, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Kaputt oder leer ergibt eine leere Liste und damit „erschoepft“, nie eine Ausnahme.</summary>
    public static IReadOnlyDictionary<string, ModelPrice> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, ModelPrice>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, ModelPrice>>(json, Json) ?? new Dictionary<string, ModelPrice>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, ModelPrice>();
        }
    }
}
