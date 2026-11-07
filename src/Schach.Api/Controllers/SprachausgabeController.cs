using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Schach.Api.Contracts;
using Schach.Api.RateLimiting;
using Schach.Application.Sprachausgabe;

namespace Schach.Api.Controllers;

/// <summary>
/// <c>GET /sprachausgabe/{token}</c> (SCHACH-SPEC 6.9): 200 <c>audio/mpeg</c>, 204 bei Stille (aus,
/// Budget, Fehler, Zeitlimit), 404 bei ungueltigem Token. Kein Partiebezug, damit der Schlussspruch
/// einer geloeschten Gast-Partie noch klingt. Per IP gedrosselt.
/// </summary>
[ApiController]
[Route(ApiJson.RoutePrefix + "/sprachausgabe")]
public sealed class SprachausgabeController(SprachausgabeDienst dienst) : ControllerBase
{
    [HttpGet("{token}")]
    [AllowAnonymous]
    [EnableRateLimiting(SchachRateLimits.SprachausgabeJeIp)]
    public async Task<IActionResult> Ton(string token, CancellationToken ct)
    {
        var ton = await dienst.AudioAsync(token, ct);
        switch (ton.Ausgang)
        {
            case SprachAusgang.Audio:
                // Inhalt haengt nur am Token: der Browser darf ihn behalten, aber nur fuer sich.
                Response.Headers.CacheControl = "private, max-age=86400";
                return File(ton.Audio!, "audio/mpeg");
            case SprachAusgang.Ungueltig:
                return NotFound();
            default:
                return NoContent();
        }
    }
}
