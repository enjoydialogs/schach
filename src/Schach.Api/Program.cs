using System.Globalization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Schach.Api.Contracts;
using Schach.Api.Middleware;
using Schach.Api.Telemetry;
using Schach.Infrastructure;
using Schach.Infrastructure.Betrieb;
using Schach.Infrastructure.Persistence;

// Program.cs ist ein Append-Register (SCHACH-SPEC 15.1 Regel 5): jede Session haengt genau einen
// DI-Block im Abschnitt "Session-Bloecke" an und aendert die Pipeline nur, wo ein Kommentar die
// Stelle nennt.

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("de-DE");

var builder = WebApplication.CreateBuilder(args);

// Wire-Format (SCHACH-SPEC 8.4a): camelCase, Enums als Strings, Zeitpunkte mit Z.
builder.Services.AddControllers().AddJsonOptions(o => ApiJson.Configure(o.JsonSerializerOptions));
builder.Services.ConfigureHttpJsonOptions(o => ApiJson.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// Tracing und Metriken: aus ohne OTEL_EXPORTER_OTLP_ENDPOINT.
var telemetryEnabled = builder.Services.AddSchachTelemetry(builder.Configuration);

// Hinter Traefik im dokploy-network. Vertraut wird NUR den Netzen aus
// ForwardedHeaders:KnownNetworks (Pflicht in Prod, Wert nur im Dokploy-Panel, SCHACH-SPEC 11.4).
// Der Wert kommt als EINE Variable, kommagetrennt; nicht ueber Get<string[]>() gebunden, das bindet
// nur indizierte Kinder. Niemals ASPNETCORE_FORWARDEDHEADERS_ENABLED: das vertraut jedem Client.
static string[] SplitDelimited(string? value) =>
    string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in SplitDelimited(builder.Configuration["ForwardedHeaders:KnownProxies"]))
    {
        o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }

    foreach (var network in SplitDelimited(builder.Configuration["ForwardedHeaders:KnownNetworks"]))
    {
        o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

// /healthz antwortet, sobald der Prozess laeuft (Container-Healthcheck, Rauchtest);
// /readyz fragt zusaetzlich die Datenbank.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SchachDbContext>("db", tags: ["ready"]);

builder.Services.AddInfrastructure(builder.Configuration);

// --- Session-Bloecke (nur anhaengen) ---
// S1a/S1b: Regeln, Uhr, Elo, Gegner, Stil
// S1c/S1d: Partie-Use-Cases, Sweep (HostedServices)
// S2a: Cookie-Authentifizierung, Antiforgery (X-CSRF-TOKEN), Magic Link, Mail
// S2b: Engine-Client, Rate-Limiter (AddRateLimiter), Meter
// S2c: Sprueche, LLM, Budgetwaechter

// S2d: Betrieb (SCHACH-SPEC 14.2): Meter "Schach", Warnzeilen, Betreibermails, Tageszeile 06:00.
builder.Services.AddSchachBetrieb(builder.Configuration);

var app = builder.Build();

if (telemetryEnabled)
{
    StartupLog.TelemetrieAn(app.Logger, SchachTracing.EndpointKey);
}
else
{
    StartupLog.TelemetrieAus(app.Logger, SchachTracing.EndpointKey);
}

app.UseExceptionHandler();
// Als Erstes, vor allem, was Schema oder Client-Adresse liest (SCHACH-SPEC 11.4).
app.UseForwardedHeaders();

// Schema beim Start (SCHACH-SPEC 13.5). Bis Session 0b die Migration 0001_Schema schreibt, ist das
// ein No-op.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SchachDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSchachSecurityHeaders();

// SPA aus wwwroot (das Web-Image kopiert den Vite-Bau dorthin). Ohne wwwroot (Entwicklung) harmlos.
var spaFiles = SpaHosting.CreateStaticFileOptions();
app.UseDefaultFiles();
app.UseStaticFiles(spaFiles);

// UseRouting MUSS nach UseStaticFiles stehen: sonst waehlt das Routing fuer jede Anfrage schon die
// Rueckfallroute, und StaticFileMiddleware tritt beiseite (Kronrat, Erst-Deploy 2026-08-10).
// Reihenfolge danach nach SCHACH-SPEC 11.4: Routing, RateLimiter (S2b), Authentication (S2a).
app.UseRouting();
// S2b: app.UseRateLimiter();
// S2a: app.UseAuthentication(); app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/healthz", new() { Predicate = _ => false });
app.MapHealthChecks("/readyz", new() { Predicate = c => c.Tags.Contains("ready") });
// /api/** und /assets/** sind von der Rueckfallroute ausgenommen (SpaHosting).
app.MapFallbackToFile(SpaHosting.FallbackRoute, "index.html", spaFiles);

await app.RunAsync();

public partial class Program;
