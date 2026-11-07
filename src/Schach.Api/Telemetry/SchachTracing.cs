using System.Diagnostics;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Schach.Infrastructure.Telemetry;

namespace Schach.Api.Telemetry;

/// <summary>
/// Tracing und Metriken ueber OTLP (Kronrat-Muster). <b>Der Endpunkt ist der Schalter:</b> ohne
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> wird nichts registriert, Entwicklung und Tests laufen ohne
/// Exporter. Endpunkt, Protokoll und Ressource liest das SDK selbst aus der Umgebung.
/// </summary>
public static class SchachTracing
{
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>Quelle fuer Spans, die die Anwendung selbst startet (Sweep).</summary>
    public const string SourceName = "Schach";

    internal static readonly ActivitySource Source = new(SourceName);

    /// <returns><c>true</c>, wenn ein Endpunkt konfiguriert ist und registriert wurde.</returns>
    public static bool AddSchachTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (string.IsNullOrWhiteSpace(configuration[EndpointKey]))
        {
            return false;
        }

        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                // Nur /api/**: Healthchecks und statische Dateien erzeugen sonst Tausende leere Spans.
                .AddAspNetCoreInstrumentation(o => o.Filter = context => IsApiRequest(context.Request.Path))
                .AddHttpClientInstrumentation()
                // Npgsql schreibt den Statement-Text, keine Parameterwerte.
                .AddNpgsql()
                .AddSource(SourceName)
                // VOR dem Exporter: Prozessoren laufen in Registrierungsreihenfolge.
                .AddProcessor<QueryStringScrubbingProcessor>()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddMeter(SchachMetrics.MeterName)
                .AddOtlpExporter());

        return true;
    }

    internal static bool IsApiRequest(PathString path) => path.StartsWithSegments("/api");
}
