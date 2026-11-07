using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Schach.Api.Auth;
using Schach.Api.Contracts;
using Schach.Api.RateLimiting;
using Schach.Application.Partien;
using Domain = Schach.Domain;
using Entitaeten = Schach.Domain.Entitaeten;

namespace Schach.Api.Controllers;

/// <summary>
/// Partie-Kern (SCHACH-SPEC 8.3, 8.5, 8.8): Starten, Laden, Zug, Zuruecknehmen, Aufgeben. Gaeste
/// spielen ohne Sitzung; ihre Partie erreichen sie ueber die 128-Bit-Zufalls-Id. Eine Spieler-Partie
/// sieht nur ihr Besitzer, fuer alle anderen gibt es sie nicht (404).
/// </summary>
[ApiController]
[Route(ApiJson.RoutePrefix + "/partien")]
public sealed class PartienController(PartieDienst dienst, TimeProvider zeit) : ControllerBase
{
    /// <summary><c>Retry-After</c> bei 429 <c>TageslimitErreicht</c>: das Limit rollt ueber 24 h.</summary>
    internal const int TageslimitRetryAfterSekunden = 3600;

    /// <summary>201 Zustand; spielt der Gegner Weiss, mit seinem ersten Zug. 409 <c>OffenePartie</c>.</summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(SchachRateLimits.GastPartieJeIp)]
    public async Task<IActionResult> Starten([FromBody] NeuePartieAnfrage anfrage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        Domain.Farbe? farbe = anfrage.Farbe switch
        {
            FarbeWahl.Weiss => Domain.Farbe.Weiss,
            FarbeWahl.Schwarz => Domain.Farbe.Schwarz,
            _ => null,
        };
        var ergebnis = await dienst.StartenAsync(
            SpielerId, anfrage.GegnerId, farbe, ZustandAbbildung.Wire<Entitaeten.Bedenkzeit>(anfrage.Bedenkzeit), ct);
        if (ergebnis is { Fehler: null, Sicht: { } sicht })
        {
            return CreatedAtAction(nameof(Laden), new { id = sicht.Partie.Id }, ZustandAbbildung.Zustand(sicht, zeit));
        }

        return Antwort(ergebnis);
    }

    /// <summary>Die offene Partie des Spielers; 204 ohne offene Partie.</summary>
    [HttpGet("offen")]
    [Authorize]
    public async Task<IActionResult> Offen(CancellationToken ct)
    {
        var sicht = SpielerId is { } id ? await dienst.OffeneAsync(id, ct) : null;
        return sicht is null ? NoContent() : Ok(ZustandAbbildung.Zustand(sicht, zeit));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Laden(Guid id, CancellationToken ct) =>
        Antwort(await dienst.LadenAsync(id, SpielerId, ct));

    /// <summary>200 Zustand mit <c>gegnerZug</c> oder <c>gegnerZugAusstehend</c>; 400 <c>ZugUnzulaessig</c>.</summary>
    [HttpPost("{id:guid}/zuege")]
    [AllowAnonymous]
    [EnableRateLimiting(SchachRateLimits.ZuegeJePartie)]
    public async Task<IActionResult> Zug(Guid id, [FromBody] ZugAnfrage anfrage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        return Antwort(await dienst.ZugAsync(id, SpielerId, anfrage.Halbzug, anfrage.Zug, ct));
    }

    [HttpPost("{id:guid}/zuruecknehmen")]
    [AllowAnonymous]
    [EnableRateLimiting(SchachRateLimits.AktionJePartie)]
    public async Task<IActionResult> Zuruecknehmen(Guid id, [FromBody] HalbzugAnfrage anfrage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        return Antwort(await dienst.ZuruecknehmenAsync(id, SpielerId, anfrage.Halbzug, ct));
    }

    [HttpPost("{id:guid}/aufgeben")]
    [AllowAnonymous]
    [EnableRateLimiting(SchachRateLimits.AktionJePartie)]
    public async Task<IActionResult> Aufgeben(Guid id, [FromBody] HalbzugAnfrage anfrage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        return Antwort(await dienst.AufgebenAsync(id, SpielerId, anfrage.Halbzug, ct));
    }

    private Guid? SpielerId => SchachSitzung.SpielerId(User);

    private IActionResult Antwort(PartieErgebnis ergebnis)
    {
        var zustand = ergebnis.Sicht is { } sicht ? ZustandAbbildung.Zustand(sicht, zeit) : null;
        switch (ergebnis.Fehler)
        {
            case null:
                return Ok(zustand);
            case PartieFehler.NichtGefunden:
                return NotFound();
            case PartieFehler.GegnerUnbekannt:
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unbekannter Gegner.");
            case PartieFehler.ZugUnzulaessig:
                return BadRequest(new FehlerAntwort(FehlerCode.ZugUnzulaessig));
            case PartieFehler.OffenePartie:
                return Conflict(new FehlerAntwort(FehlerCode.OffenePartie, null, ergebnis.OffenePartieId));
            case PartieFehler.TageslimitErreicht:
                Response.Headers.RetryAfter = TageslimitRetryAfterSekunden.ToString(CultureInfo.InvariantCulture);
                return StatusCode(StatusCodes.Status429TooManyRequests, new FehlerAntwort(FehlerCode.TageslimitErreicht));
            case PartieFehler.GeradeVielLos:
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new FehlerAntwort(FehlerCode.GeradeVielLos));
            default:
                return Conflict(new FehlerAntwort(ZustandAbbildung.Wire<FehlerCode>(ergebnis.Fehler.Value), zustand));
        }
    }
}
