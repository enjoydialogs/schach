using System.Globalization;
using System.Text;

namespace Schach.Domain;

/// <summary>Kopfdaten einer Partie fuer das PGN.</summary>
/// <param name="Weiss">Name auf der weissen Seite (Gegner-Name oder Spieler-Anzeige).</param>
/// <param name="Schwarz">Name auf der schwarzen Seite.</param>
/// <param name="Datum">Tag des Partiebeginns.</param>
/// <param name="StartFen">Startstellung, falls nicht die Grundstellung.</param>
public sealed record PgnKopf(string Weiss, string Schwarz, DateOnly Datum, string? StartFen = null);

/// <summary>
/// Erzeugt PGN aus der Zugfolge (SCHACH-SPEC 3.6). Der Grund steht als Enum-Name im Tag
/// <c>Termination</c>. Zeilenende <c>\n</c>, SAN nach PGN-Standard (chess.js liest es fuer
/// Nachspielen und "PGN teilen").
/// </summary>
public static class Pgn
{
    /// <summary>Wert des Tags <c>Event</c>.</summary>
    public const string Event = "Schach";

    /// <summary>Wert des Tags <c>Site</c>.</summary>
    public const string Site = "schach.enjoy-dialogs.de";

    /// <summary>Erzeugt das PGN. <paramref name="ende"/> <c>null</c>: Partie offen, Result <c>*</c>.</summary>
    /// <exception cref="ZugUnzulaessigException">Ein Zug der Zugfolge ist nicht erlaubt.</exception>
    public static string Erzeuge(PgnKopf kopf, IEnumerable<string> zugfolge, Partieende? ende)
    {
        ArgumentNullException.ThrowIfNull(kopf);
        ArgumentNullException.ThrowIfNull(zugfolge);

        var result = ende?.Ergebnis switch
        {
            Ergebnis.WeissGewinnt => "1-0",
            Ergebnis.SchwarzGewinnt => "0-1",
            Ergebnis.Remis => "1/2-1/2",
            _ => "*",
        };

        var sb = new StringBuilder();
        Tag(sb, "Event", Event);
        Tag(sb, "Site", Site);
        Tag(sb, "Date", kopf.Datum.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture));
        Tag(sb, "Round", "-");
        Tag(sb, "White", kopf.Weiss);
        Tag(sb, "Black", kopf.Schwarz);
        Tag(sb, "Result", result);
        var startFen = kopf.StartFen ?? SchachDomain.GrundstellungFen;
        if (startFen != SchachDomain.GrundstellungFen)
        {
            Tag(sb, "SetUp", "1");
            Tag(sb, "FEN", startFen);
        }

        if (ende is not null)
        {
            Tag(sb, "Termination", ende.Grund.ToString());
        }

        sb.Append('\n');

        var stellung = Stellung.AusFen(startFen);
        var zugnummer = int.Parse(startFen.Split(' ')[5], CultureInfo.InvariantCulture);
        var erster = true;
        foreach (var uci in zugfolge)
        {
            if (stellung.AmZug == Farbe.Weiss)
            {
                sb.Append(CultureInfo.InvariantCulture, $"{zugnummer}. ");
            }
            else if (erster)
            {
                sb.Append(CultureInfo.InvariantCulture, $"{zugnummer}... ");
            }

            sb.Append(stellung.San(uci)).Append(' ');
            if (stellung.AmZug == Farbe.Schwarz)
            {
                zugnummer++;
            }

            stellung = stellung.Ziehe(uci);
            erster = false;
        }

        sb.Append(result).Append('\n');
        return sb.ToString();
    }

    private static void Tag(StringBuilder sb, string name, string wert)
    {
        var maskiert = wert.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        sb.Append(CultureInfo.InvariantCulture, $"[{name} \"{maskiert}\"]\n");
    }
}
