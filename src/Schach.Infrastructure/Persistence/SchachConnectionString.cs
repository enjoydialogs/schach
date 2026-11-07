using Npgsql;

namespace Schach.Infrastructure.Persistence;

/// <summary>Liest und normalisiert die Verbindungszeichenfolge (SCHACH-SPEC 13.1).</summary>
public static class SchachConnectionString
{
    /// <summary>Name unter <c>ConnectionStrings</c>, als Umgebungsvariable <c>ConnectionStrings__Schach</c>.</summary>
    public const string Name = "Schach";

    /// <summary>Umgebungsvariable, die auch <c>dotnet ef</c> liest.</summary>
    public const string Variable = "ConnectionStrings__" + Name;

    /// <summary>
    /// Gibt die Zeichenfolge mit <c>GSS Encryption Mode=Disable</c> zurueck, falls der Modus nicht
    /// gesetzt ist. Ohne diese Vorgabe versucht Npgsql zuerst eine GSS-Verschluesselung, die der
    /// Postgres-Container nicht anbietet. Leer oder fehlend: Ausnahme mit Variablenname.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Keine Datenbank konfiguriert: {Variable} ist nicht gesetzt " +
                "(Beispiel: Host=localhost;Port=5432;Database=schach;Username=schach;Password=...).");
        }

        var builder = new NpgsqlConnectionStringBuilder(value);
        if (!value.Contains("GSS Encryption Mode", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("GssEncryptionMode", StringComparison.OrdinalIgnoreCase))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }
}
