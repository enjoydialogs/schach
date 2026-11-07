using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Schach.Api.Auth;
using Schach.Api.Contracts;
using Schach.Infrastructure.Identity;

namespace Schach.Api.Controllers;

/// <summary>Anmeldung per Magic Link oder Code und Sitzung (SCHACH-SPEC 9.3).</summary>
[ApiController]
[Route(ApiJson.RoutePrefix)]
public sealed class AuthController(AnmeldeDienst anmeldung, KontoDienst konto, IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>Immer 202 <c>{anmeldungId}</c>, ausser bei Formfehler, Versandfehler oder Kontingent.</summary>
    [HttpPost("auth/anmeldung")]
    [AllowAnonymous]
    [EnableRateLimiting(SchachSitzung.MagicLinkJeIp)]
    public async Task<IActionResult> Anfordern([FromBody] AnmeldungAnfrage anfrage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        var (ergebnis, id) = await anmeldung.AnfordernAsync(anfrage.Email, ct);
        return ergebnis switch
        {
            AnfordernErgebnis.Angenommen => Accepted(new AnmeldungAntwort(id)),
            AnfordernErgebnis.EmailUngueltig => BadRequest(new FehlerAntwort(FehlerCode.EmailUngueltig)),
            AnfordernErgebnis.KontingentErschoepft => StatusCode(StatusCodes.Status503ServiceUnavailable, new FehlerAntwort(FehlerCode.MailKontingentErschoepft)),
            _ => StatusCode(StatusCodes.Status503ServiceUnavailable, new FehlerAntwort(FehlerCode.MailNichtGesendet)),
        };
    }

    /// <summary>200 <c>{spieler, neu}</c> plus Cookie; jede Ablehnung 422 <c>AnmeldungUngueltig</c>.</summary>
    [HttpPost("auth/anmeldung/bestaetigen")]
    [AllowAnonymous]
    public async Task<IActionResult> Bestaetigen([FromBody] BestaetigenAnfrage anfrage, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(anfrage);
        Angemeldet? angemeldet = null;
        if (!string.IsNullOrEmpty(anfrage.Token))
        {
            angemeldet = await anmeldung.BestaetigenPerTokenAsync(anfrage.Token, ct);
        }
        else if (anfrage.AnmeldungId is { } id && !string.IsNullOrEmpty(anfrage.Code))
        {
            angemeldet = await anmeldung.BestaetigenPerCodeAsync(id, anfrage.Code, ct);
        }

        if (angemeldet is null)
        {
            return UnprocessableEntity(new FehlerAntwort(FehlerCode.AnmeldungUngueltig));
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            SchachSitzung.Principal(angemeldet.Spieler),
            new AuthenticationProperties { IsPersistent = true });

        var s = angemeldet.Spieler;
        return Ok(new BestaetigenAntwort(new SpielerDto(s.Id, s.Email, s.Elo), angemeldet.Neu));
    }

    /// <summary><c>{angemeldet, spieler?, csrfToken}</c>; setzt das Antiforgery-Cookie.</summary>
    [HttpGet("sitzung")]
    [AllowAnonymous]
    public async Task<SitzungAntwort> Sitzung(CancellationToken ct)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        var spieler = SchachSitzung.SpielerId(User) is { } id ? await konto.SpielerAsync(id, ct) : null;
        return new SitzungAntwort(
            spieler is not null,
            spieler is null ? null : new SpielerDto(spieler.Id, spieler.Email, spieler.Elo),
            tokens.RequestToken!);
    }

    /// <summary>204. <c>ueberall</c> erneuert den Sicherheitsstempel: alle Sitzungen enden.</summary>
    [HttpPost("auth/abmelden")]
    [AllowAnonymous]
    public async Task<IActionResult> Abmelden([FromBody] AbmeldenAnfrage? anfrage, CancellationToken ct)
    {
        if (anfrage?.Ueberall == true && SchachSitzung.SpielerId(User) is { } id)
        {
            await konto.StempelErneuernAsync(id, ct);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
