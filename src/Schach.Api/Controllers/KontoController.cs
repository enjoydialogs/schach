using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schach.Api.Auth;
using Schach.Api.Contracts;
using Schach.Infrastructure.Identity;
using Domain = Schach.Domain;
using Entitaeten = Schach.Domain.Entitaeten;

namespace Schach.Api.Controllers;

/// <summary>Konto loeschen und exportieren (SCHACH-SPEC 9.3, 9.7, 12.4).</summary>
[ApiController]
[Route(ApiJson.RoutePrefix + "/konto")]
[Authorize]
public sealed class KontoController(KontoDienst konto) : ControllerBase
{
    /// <summary>Loescht hart und sofort, dann abmelden. Andere Sitzungen sterben an der Stempelpruefung.</summary>
    [HttpDelete]
    public async Task<IActionResult> Loeschen(CancellationToken ct)
    {
        if (SchachSitzung.SpielerId(User) is { } id)
        {
            await konto.LoeschenAsync(id, ct);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>JSON: Konto, Elo, Bilanz je Gegner, alle Partien mit PGN und Spruechen.</summary>
    [HttpGet("export")]
    public async Task<ActionResult<KontoExportDto>> Export(CancellationToken ct)
    {
        var daten = SchachSitzung.SpielerId(User) is { } id ? await konto.ExportAsync(id, ct) : null;
        if (daten is null)
        {
            return Unauthorized();
        }

        var s = daten.Spieler;
        return new KontoExportDto(
            new KontoExportKonto(s.Id, s.Email, s.ErstelltAm, s.ZuletztAngemeldetAm),
            s.Elo,
            [.. daten.Bilanz.Select(b => new KontoExportBilanz(b.GegnerId, b.Siege, b.Remis, b.Niederlagen, b.BesiegtAm))],
            [.. daten.Partien.Select(Partie)]);
    }

    private static KontoExportPartie Partie(ExportPartie e)
    {
        var p = e.Partie;
        return new KontoExportPartie(
            p.Id,
            p.GegnerId,
            Enum.Parse<Farbe>(p.SpielerFarbe.ToString()),
            Enum.Parse<Bedenkzeit>(p.Bedenkzeit.ToString()),
            Enum.Parse<PartieStatus>(p.Status.ToString()),
            p.Ergebnis is { } erg ? Enum.Parse<Ergebnis>(erg.ToString()) : null,
            p.Grund is { } g ? Enum.Parse<Grund>(g.ToString()) : null,
            p.ErstelltAm,
            p.BeendetAm,
            p.ZaehltFuerElo,
            p.EloVorher,
            p.EloNachher,
            p.Status == Entitaeten.PartieStatus.Beendet ? ExportPgn(e) : null,
            [.. e.Sprueche.Select(sp => new SpruchDto(sp.NachHalbzug, Enum.Parse<SpruchEreignis>(sp.Ereignis.ToString()), sp.Text))]);
    }

    /// <summary>PGN ueber <see cref="Domain.Pgn"/> aus der gespeicherten Zugfolge (UCI).</summary>
    internal static string ExportPgn(ExportPartie e)
    {
        var p = e.Partie;
        var gegner = Domain.GegnerKatalog.Finde(p.GegnerId)?.Name ?? p.GegnerId;
        var (weiss, schwarz) = p.SpielerFarbe == Domain.Farbe.Weiss ? ("Spieler", gegner) : (gegner, "Spieler");
        var ende = p.Ergebnis is { } erg && p.Grund is { } grund
            ? new Domain.Partieende(erg, grund)
            : null;
        return Domain.Pgn.Erzeuge(new Domain.PgnKopf(weiss, schwarz, DateOnly.FromDateTime(p.ErstelltAm.UtcDateTime)), e.Zuege.Select(z => z.Uci), ende);
    }
}
