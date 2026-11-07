using Schach.Api.Contracts;
using Schach.Application.Partien;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Identity;
using Domain = Schach.Domain;

namespace Schach.Api.Controllers;

/// <summary>
/// Domaene nach Vertrag (SCHACH-SPEC 8.4, 8.4a). Domaenen- und Wire-Enums tragen dieselben Namen;
/// die Abbildung geht ueber den Namen (<c>WireEnumsTests</c> haelt sie gleich).
/// </summary>
internal static class ZustandAbbildung
{
    public static TZiel Wire<TZiel>(Enum wert)
        where TZiel : struct, Enum => Enum.Parse<TZiel>(wert.ToString());

    public static ZustandDto Zustand(PartieSicht sicht, TimeProvider zeit)
    {
        ArgumentNullException.ThrowIfNull(sicht);
        var p = sicht.Partie;
        var gegner = Domain.GegnerKatalog.Finde(p.GegnerId) ?? throw new InvalidOperationException($"Unbekannter Gegner '{p.GegnerId}'.");
        var beendet = p.Status == Schach.Domain.Entitaeten.PartieStatus.Beendet;
        return new ZustandDto(
            p.Id,
            Wire<Contracts.PartieStatus>(p.Status),
            Wire<Contracts.Farbe>(p.SpielerFarbe),
            Gegner(gegner, null),
            Wire<Contracts.Bedenkzeit>(p.Bedenkzeit),
            p.Halbzuege,
            p.Fen,
            [.. sicht.Zugfolge.OrderBy(z => z.Halbzug).Select(z => new ZugDto(z.Halbzug, Wire<Contracts.Farbe>(z.Seite), z.Uci, z.San, z.GespieltAm))],
            p.SpielerAmZug() ? Domain.Stellung.AusFen(p.Fen).ErlaubteZuege() : [],
            p.SpielerAmZug(),
            p.GegnerZugAusstehend(),
            sicht.GegnerZug is { } g ? GegnerZug(g, sicht.Sprueche) : null,
            Uhr(p, zeit),
            p.ZaehltFuerElo,
            p.LetztesRemisAngebotBeiZug is { } letztes ? letztes + 5 : 1,
            [.. sicht.Sprueche.Select(Spruch)],
            p.Ergebnis is { } erg ? Wire<Contracts.Ergebnis>(erg) : null,
            p.Grund is { } grund ? Wire<Contracts.Grund>(grund) : null,
            p.EloVorher,
            p.EloNachher,
            beendet ? KontoController.ExportPgn(new ExportPartie(p, sicht.Zugfolge, sicht.Sprueche)) : null);
    }

    public static GegnerDto Gegner(Domain.Gegner gegner, Bilanz? bilanz)
    {
        ArgumentNullException.ThrowIfNull(gegner);
        return new GegnerDto(
            gegner.Key,
            gegner.Name,
            gegner.Elo,
            Wire<Contracts.Stufe>(gegner.Stufe),
            Wire<Contracts.Persoenlichkeit>(gegner.Persoenlichkeit),
            gegner.Vorstellung,
            bilanz is null ? null : new BilanzDto(bilanz.Siege, bilanz.Remis, bilanz.Niederlagen, bilanz.BesiegtAm));
    }

    private static GegnerZugDto GegnerZug(Zug zug, IReadOnlyList<Spruch> sprueche)
    {
        var spruch = sprueche.LastOrDefault(s => s.NachHalbzug == zug.Halbzug);
        return new GegnerZugDto(zug.Halbzug, Wire<Contracts.Farbe>(zug.Seite), zug.Uci, zug.San, zug.GespieltAm, zug.GespieltAm, spruch is null ? null : Spruch(spruch));
    }

    private static SpruchDto Spruch(Spruch s) => new(s.NachHalbzug, Wire<Contracts.SpruchEreignis>(s.Ereignis), s.Text);

    /// <summary>Uhrstand jetzt; nach Partieende steht die Uhr (<c>laeuftFuer = null</c>).</summary>
    private static UhrDto? Uhr(Partie p, TimeProvider zeit)
    {
        if (p.Uhr() is not { } uhr)
        {
            return null;
        }

        var jetzt = zeit.GetUtcNow();
        if (p.Status == Schach.Domain.Entitaeten.PartieStatus.Beendet)
        {
            return new UhrDto(uhr.SpielerRestMs, uhr.GegnerRestMs, null, null, jetzt);
        }

        return new UhrDto(
            uhr.RestMs(Domain.UhrSeite.Spieler, zeit),
            uhr.RestMs(Domain.UhrSeite.Gegner, zeit),
            Wire<Contracts.UhrSeite>(uhr.LaeuftFuer),
            uhr.AmZugSeit,
            jetzt);
    }
}
