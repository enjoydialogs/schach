using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Schach.Application.Limits;
using Schach.Infrastructure.Telemetry;

namespace Schach.Api.RateLimiting;

/// <summary>
/// Rate-Limiter nach SCHACH-SPEC 11.4. Controller haengen die Policies per
/// <c>[EnableRateLimiting(SchachRateLimits.…)]</c> an:
/// <list type="bullet">
/// <item><see cref="MagicLinkJeIp"/>: <c>POST /auth/anmeldung</c> (S2a).</item>
/// <item><see cref="GastPartieJeIp"/>: <c>POST /partien</c> (S1c).</item>
/// <item><see cref="ZuegeJePartie"/>: <c>POST /partien/{id}/zuege</c>; <see cref="TippJePartie"/>:
/// <c>POST /partien/{id}/tipp</c>; <see cref="AktionJePartie"/>: zuruecknehmen, aufgeben, remis-angebot.</item>
/// </list>
/// Der GlobalLimiter (Kette zweier ConcurrencyLimiter) greift ohne Attribut auf jedes
/// <c>POST …/zuege</c> und <c>POST …/tipp</c>.
/// IPs leben nur im Speicher der Limiter, nie im Meter und nie in Logs.
/// </summary>
public static class SchachRateLimits
{
    public const string MagicLinkJeIp = "magic-link-je-ip";
    public const string GastPartieJeIp = "gast-partie-je-ip";
    public const string ZuegeJePartie = "zuege-je-partie";
    public const string TippJePartie = "tipp-je-partie";
    public const string AktionJePartie = "aktion-je-partie";

    /// <summary><c>GET /sprachausgabe/{token}</c> (SCHACH-SPEC 6.9), Sliding 1 min je IP.</summary>
    public const string SprachausgabeJeIp = "sprachausgabe-je-ip";

    /// <summary>Metrik-Name fuer Ablehnungen des GlobalLimiters auf <c>…/zuege</c>.</summary>
    public const string EngineZuegeGleichzeitig = "engine-zuege-gleichzeitig";

    /// <summary>Metrik-Name fuer Ablehnungen des GlobalLimiters auf <c>…/tipp</c>.</summary>
    public const string StockfishTippsGleichzeitig = "stockfish-tipps-gleichzeitig";

    /// <summary><c>Retry-After</c>, wenn der Limiter keinen eigenen Wert kennt (SCHACH-SPEC 11.2, 11.4).</summary>
    public const int StandardRetryAfterSekunden = 2;

    private const string OhneAdresse = "ohne-adresse";
    private static readonly object GlobalAbgelehntKey = new();

    public static IServiceCollection AddSchachRateLimiter(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<LimitsOptions>(configuration.GetSection(LimitsOptions.Abschnitt));
        services.AddMetrics();
        services.TryAddSingleton<SchachZaehler>();

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<LimitsOptions>, SchachZaehler>((o, limitsOptions, zaehler) =>
            {
                var limits = limitsOptions.Value;
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                o.AddPolicy(MagicLinkJeIp, context => RateLimitPartition.GetFixedWindowLimiter(
                    ClientAdresse(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.AnmeldungenJeIpUnd10Minuten,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                    }));

                // Angemeldete Spieler ohne Limit. Achtung: context.User ist nur gefuellt, wenn die
                // Anmeldung vor dem Limiter ausgewertet wurde (Issue #32, offener Punkt fuer S2a).
                o.AddPolicy(GastPartieJeIp, context => context.User.Identity?.IsAuthenticated == true
                    ? RateLimitPartition.GetNoLimiter("spieler")
                    : RateLimitPartition.GetSlidingWindowLimiter(
                        ClientAdresse(context),
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = limits.GastPartienJeIpUndStunde,
                            Window = TimeSpan.FromHours(1),
                            SegmentsPerWindow = 6,
                            QueueLimit = 0,
                        }));

                o.AddPolicy(ZuegeJePartie, context => JePartie(context, limits.ZuegeJePartieUndMinute));
                o.AddPolicy(TippJePartie, context => JePartie(context, limits.TippsJePartieUndMinute));
                o.AddPolicy(AktionJePartie, context => JePartie(context, limits.AktionenJePartieUndMinute));
                o.AddPolicy(SprachausgabeJeIp, context => RateLimitPartition.GetSlidingWindowLimiter(
                    ClientAdresse(context),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = limits.SprachausgabenJeIpUndMinute,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    }));

                o.GlobalLimiter = new MarkierenderLimiter(PartitionedRateLimiter.CreateChained(
                    Gleichzeitig("/zuege", EngineZuegeGleichzeitig, limits.EngineZuegeGleichzeitig, limits.EngineZuegeWarteschlange),
                    Gleichzeitig("/tipp", StockfishTippsGleichzeitig, limits.StockfishTippsGleichzeitig, limits.StockfishTippsWarteschlange)));

                o.OnRejected = (context, _) =>
                {
                    var policy = context.HttpContext.Items[GlobalAbgelehntKey] as string
                        ?? context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
                        ?? "unbekannt";
                    zaehler.LimitAbgelehnt(policy);

                    var sekunden = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                        : StandardRetryAfterSekunden;
                    context.HttpContext.Response.Headers.RetryAfter = sekunden.ToString(CultureInfo.InvariantCulture);
                    return ValueTask.CompletedTask;
                };
            });

        return services;
    }

    private static string ClientAdresse(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? OhneAdresse;

    /// <summary>Sliding 1 min, Partition = Routen-<c>id</c> (gibt es erst nach <c>UseRouting</c>).</summary>
    private static RateLimitPartition<string> JePartie(HttpContext context, int jeMinute) =>
        RateLimitPartition.GetSlidingWindowLimiter(
            context.GetRouteValue("id")?.ToString() ?? "ohne-id",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = jeMinute,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
            });

    private static PartitionedRateLimiter<HttpContext> Gleichzeitig(string endung, string name, int gleichzeitig, int warteschlange) =>
        PartitionedRateLimiter.Create<HttpContext, string>(context => IstPostAuf(context, endung)
            ? RateLimitPartition.GetConcurrencyLimiter(name, _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = gleichzeitig,
                QueueLimit = warteschlange,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            })
            : RateLimitPartition.GetNoLimiter(""));

    private static bool IstPostAuf(HttpContext context, string endung) =>
        HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.Value?.TrimEnd('/').EndsWith(endung, StringComparison.OrdinalIgnoreCase) == true;

    private static string GlobalPolicyName(HttpContext context) =>
        IstPostAuf(context, "/tipp") ? StockfishTippsGleichzeitig : EngineZuegeGleichzeitig;

    /// <summary>Merkt am Request, dass der GlobalLimiter (nicht die Endpunkt-Policy) abgelehnt hat,
    /// damit <c>OnRejected</c> den richtigen Policy-Namen zaehlt.</summary>
    private sealed class MarkierenderLimiter(PartitionedRateLimiter<HttpContext> inner) : PartitionedRateLimiter<HttpContext>
    {
        public override RateLimiterStatistics? GetStatistics(HttpContext resource) => inner.GetStatistics(resource);

        protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount) =>
            Markiere(resource, inner.AttemptAcquire(resource, permitCount));

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(HttpContext resource, int permitCount, CancellationToken cancellationToken) =>
            Markiere(resource, await inner.AcquireAsync(resource, permitCount, cancellationToken));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private static RateLimitLease Markiere(HttpContext context, RateLimitLease lease)
        {
            if (lease.IsAcquired)
            {
                context.Items.Remove(GlobalAbgelehntKey);
            }
            else
            {
                context.Items[GlobalAbgelehntKey] = GlobalPolicyName(context);
            }

            return lease;
        }
    }
}
