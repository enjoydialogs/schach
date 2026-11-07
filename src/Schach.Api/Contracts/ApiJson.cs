using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Schach.Api.Contracts;

/// <summary>
/// Wire-Format der API (SCHACH-SPEC 8.4a), an einer Stelle fuer Program.cs und die Vertragstests:
/// camelCase, Enums als PascalCase-Strings, Zeitpunkte als ISO-8601 in UTC mit <c>Z</c>.
/// </summary>
public static class ApiJson
{
    /// <summary>Routenpraefix aller Endpunkte.</summary>
    public const string RoutePrefix = "api/v1";

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        // Ohne Naming Policy: der Enum-Name ist der Draht-Wert (PascalCase). Zahlen sind verboten.
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.Converters.Add(new UtcDateTimeOffsetConverter());
    }

    /// <summary>Neue Optionen mit Web-Vorgaben plus <see cref="Configure"/>.</summary>
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    /// <summary>Schreibt jeden <see cref="DateTimeOffset"/> nach UTC normalisiert mit <c>Z</c>
    /// (System.Text.Json schriebe sonst <c>+00:00</c>). Liest jede ISO-8601-Form.</summary>
    internal sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToUniversalTime();

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime.ToString(Format, CultureInfo.InvariantCulture));
    }
}
