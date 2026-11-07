namespace Schach.Api.RateLimiting;

/// <summary>
/// Warnt beim Start, wenn <c>ForwardedHeaders:KnownNetworks</c> fehlt (SCHACH-SPEC 11.4). Ohne den
/// Wert verwirft <c>UseForwardedHeaders</c> <c>X-Forwarded-For</c>, und alle IP-Limiter sehen nur
/// Traefik: dann teilen sich alle Gaeste ein Limit. Kein harter Abbruch, weil das lokale
/// <c>docker-compose.yml</c> (Rauchtest in CI) ohne Proxy laeuft; in Prod erzwingt
/// <c>docker-compose.dokploy.yml</c> den Wert (<c>TRAEFIK_NETWORK</c>).
/// </summary>
internal sealed partial class ForwardedHeadersPruefung(
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<ForwardedHeadersPruefung> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (FehltVertrauensnetz(configuration, environment.EnvironmentName))
        {
            OhneVertrauensnetz(logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static bool FehltVertrauensnetz(IConfiguration configuration, string umgebung) =>
        umgebung is not ("Development" or "Testing")
        && string.IsNullOrWhiteSpace(configuration["ForwardedHeaders:KnownNetworks"]);

    [LoggerMessage(EventName = "ForwardedHeadersOhneVertrauensnetz", Level = LogLevel.Warning,
        Message = "ForwardedHeaders:KnownNetworks fehlt: IP-Limiter sehen nur den Proxy (SCHACH-SPEC 11.4).")]
    private static partial void OhneVertrauensnetz(ILogger logger);
}
