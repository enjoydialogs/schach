using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Identity;

namespace Schach.Api.Auth;

/// <summary>
/// Sitzung per Cookie <c>schach_sitzung</c>, Stempelpruefung und CSRF (SCHACH-SPEC 9.1, 9.4) plus
/// den Namen des Limiters <c>magic-link-je-ip</c> (SCHACH-SPEC 11.3, 11.4; registriert in <c>SchachRateLimits</c>).
/// </summary>
internal static partial class SchachSitzung
{
    public const string CookieName = "schach_sitzung";
    public const string CsrfHeader = "X-CSRF-TOKEN";
    public const string ClaimSpieler = "sub";
    public const string ClaimStempel = "stempel";
    public const string MagicLinkJeIp = RateLimiting.SchachRateLimits.MagicLinkJeIp;

    public static IServiceCollection AddSchachSitzung(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = CookieName;
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.IsEssential = true;
                o.ExpireTimeSpan = TimeSpan.FromDays(180);
                o.SlidingExpiration = true;
                o.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                o.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                o.Events.OnValidatePrincipal = StempelPruefenAsync;
            });
        services.AddAuthorization();

        services.AddAntiforgery(o => o.HeaderName = CsrfHeader);
        // AddControllers bringt die ViewFeatures nicht mit; dort lebt der Filter hinter
        // AutoValidateAntiforgeryTokenAttribute. AddViews registriert ihn (ohne Razor-Seiten).
        services.AddMvcCore().AddViews();
        services.Configure<MvcOptions>(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

        // Der Limiter magic-link-je-ip steht mit allen anderen Policies in SchachRateLimits (S2b),
        // damit OnRejected (Retry-After, Meter) fuer alle gilt und keine Policy doppelt registriert wird.
        return services;
    }

    /// <summary>Claims fuer <c>SignInAsync</c>: <c>sub</c> und <c>stempel</c>.</summary>
    public static ClaimsPrincipal Principal(Spieler spieler) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimSpieler, spieler.Id.ToString()),
                new Claim(ClaimStempel, spieler.Sicherheitsstempel.ToString()),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimSpieler,
            null));

    public static Guid? SpielerId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && Guid.TryParse(user.FindFirst(ClaimSpieler)?.Value, out var id) ? id : null;

    /// <summary>Liest den Stempel per PK. Fehlt der Spieler, weicht der Stempel ab oder scheitert die
    /// Datenbank: ablehnen und abmelden (SCHACH-SPEC 9.4).</summary>
    private static async Task StempelPruefenAsync(CookieValidatePrincipalContext ctx)
    {
        var gueltig = false;
        try
        {
            if (ctx.Principal is { } p
                && SpielerId(p) is { } id
                && Guid.TryParse(p.FindFirst(ClaimStempel)?.Value, out var stempel))
            {
                var konto = ctx.HttpContext.RequestServices.GetRequiredService<KontoDienst>();
                gueltig = await konto.StempelAsync(id, ctx.HttpContext.RequestAborted) == stempel;
            }
        }
#pragma warning disable CA1031 // Jeder Fehler beim Pruefen heisst: Sitzung ablehnen.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SchachSitzung));
            StempelFehler(log, ex.GetType().Name);
        }

        if (!gueltig)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "sitzung stempelpruefung fehlgeschlagen fehler={Fehler}")]
    private static partial void StempelFehler(ILogger log, string fehler);
}
