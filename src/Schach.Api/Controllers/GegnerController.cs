using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Schach.Api.Auth;
using Schach.Api.Contracts;
using Schach.Application.Partien;

namespace Schach.Api.Controllers;

/// <summary><c>GET /gegner</c> (SCHACH-SPEC 4.3, 4.5, 8.3): alle Gegner Elo aufsteigend, Bilanz nur fuer Spieler.</summary>
[ApiController]
[Route(ApiJson.RoutePrefix + "/gegner")]
public sealed class GegnerController(PartieDienst dienst) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IReadOnlyList<GegnerDto>> Alle(CancellationToken ct) =>
        [.. (await dienst.GegnerAsync(SchachSitzung.SpielerId(User), ct)).Select(g => ZustandAbbildung.Gegner(g.Gegner, g.Bilanz))];
}
