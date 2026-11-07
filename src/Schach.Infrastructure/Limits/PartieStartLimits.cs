using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Schach.Application.Abstractions;
using Schach.Application.Limits;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Engine;
using Schach.Infrastructure.Telemetry;

namespace Schach.Infrastructure.Limits;

/// <summary>
/// <see cref="IPartieStartLimits"/> ueber die Datenbank (SCHACH-SPEC 11.2). Das Tageslimit zaehlt
/// rollierend die letzten 24 h nach <c>Partie.ErstelltAm</c>, alle Status. Geloeschte Gast-Partien
/// zaehlen naturgemaess nicht. Jede Ablehnung zaehlt im Meter <c>schach.limits.abgelehnt</c>.
/// </summary>
internal sealed class PartieStartLimits(
    IAppDbContext db,
    IOptions<LimitsOptions> options,
    EngineUeberlastWaechter waechter,
    TimeProvider zeit,
    SchachZaehler zaehler) : IPartieStartLimits
{
    internal const string PolicyTageslimit = "partien-je-spieler-und-tag";
    internal const string PolicyGeradeVielLos = "offene-gast-partien";
    internal const string PolicyUeberlast = "engine-ueberlast";

    public async Task<PartieStartAblehnung?> PruefeAsync(Guid? spielerId, CancellationToken cancellationToken = default)
    {
        var limits = options.Value;
        var partien = db.Set<Partie>().AsNoTracking();
        PartieStartAblehnung? ablehnung;
        string policy;

        if (spielerId is { } id)
        {
            var seit = zeit.GetUtcNow().AddHours(-24);
            var anzahl = await partien.CountAsync(p => p.SpielerId == id && p.ErstelltAm > seit, cancellationToken);
            ablehnung = Entscheide(istGast: false, anzahl, 0, engineUeberlastet: false, limits);
            policy = PolicyTageslimit;
        }
        else if (waechter.IstGesperrt)
        {
            ablehnung = PartieStartAblehnung.GeradeVielLos;
            policy = PolicyUeberlast;
        }
        else
        {
            var offen = await partien.CountAsync(p => p.SpielerId == null && p.Status == PartieStatus.Offen, cancellationToken);
            ablehnung = Entscheide(istGast: true, 0, offen, engineUeberlastet: false, limits);
            policy = PolicyGeradeVielLos;
        }

        if (ablehnung is not null)
        {
            zaehler.LimitAbgelehnt(policy);
        }

        return ablehnung;
    }

    /// <summary>Reine Entscheidung aus den Zaehlungen (SCHACH-SPEC 11.2).</summary>
    internal static PartieStartAblehnung? Entscheide(
        bool istGast, int partienLetzte24h, int offeneGastPartien, bool engineUeberlastet, LimitsOptions limits)
    {
        if (!istGast)
        {
            return partienLetzte24h >= limits.PartienJeSpielerUndTag ? PartieStartAblehnung.TageslimitErreicht : null;
        }

        return engineUeberlastet || offeneGastPartien >= limits.OffeneGastPartienGesamt
            ? PartieStartAblehnung.GeradeVielLos
            : null;
    }
}
