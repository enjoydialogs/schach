using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Schach.Application.Limits;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Engine;
using Schach.Infrastructure.Limits;
using Schach.Infrastructure.Persistence;
using Schach.Infrastructure.Telemetry;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests.Limits;

/// <summary>Die Datenbankzaehlungen hinter <see cref="IPartieStartLimits"/> gegen Postgres 17.</summary>
public sealed class PartieStartLimitsPostgresTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Zaehlt_Spieler_Partien_der_letzten_24_Stunden_und_offene_Gast_Partien()
    {
        var cs = await SharedPostgres.CreateDatabaseAsync("schach_limits");
        try
        {
            var spieler = new Spieler { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@example.org", ErstelltAm = Jetzt, Sicherheitsstempel = Guid.NewGuid(), Elo = 800 };
            await using (var db = Kontext(cs))
            {
                await db.Database.MigrateAsync(Ct);
                db.Add(spieler);
                db.Add(Partie(spieler.Id, PartieStatus.Beendet, Jetzt.AddHours(-1)));
                db.Add(Partie(spieler.Id, PartieStatus.Offen, Jetzt.AddHours(-2)));
                db.Add(Partie(spieler.Id, PartieStatus.Beendet, Jetzt.AddHours(-25)));
                db.Add(Partie(null, PartieStatus.Offen, Jetzt.AddHours(-1)));
                db.Add(Partie(null, PartieStatus.Beendet, Jetzt.AddHours(-1)));
                await db.SaveChangesAsync(Ct);
            }

            await using var pruef = Kontext(cs);
            using var messwerte = new Messwerte();

            (await Limits(pruef, messwerte, new LimitsOptions { PartienJeSpielerUndTag = 2 }).PruefeAsync(spieler.Id, Ct))
                .Should().Be(PartieStartAblehnung.TageslimitErreicht);
            (await Limits(pruef, messwerte, new LimitsOptions { PartienJeSpielerUndTag = 3 }).PruefeAsync(spieler.Id, Ct))
                .Should().BeNull();
            (await Limits(pruef, messwerte, new LimitsOptions { OffeneGastPartienGesamt = 1 }).PruefeAsync(null, Ct))
                .Should().Be(PartieStartAblehnung.GeradeVielLos);
            (await Limits(pruef, messwerte, new LimitsOptions { OffeneGastPartienGesamt = 2 }).PruefeAsync(null, Ct))
                .Should().BeNull();

            messwerte.Von(SchachMetrics.LimitsAbgelehnt).Select(t => t["policy"])
                .Should().Equal(PartieStartLimits.PolicyTageslimit, PartieStartLimits.PolicyGeradeVielLos);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(cs);
        }
    }

    private static PartieStartLimits Limits(SchachDbContext db, Messwerte messwerte, LimitsOptions options)
    {
        var zeit = new FakeTimeProvider(Jetzt);
        var waechter = new EngineUeberlastWaechter(Options.Create(new EngineUeberlastOptions()), zeit, NullLogger<EngineUeberlastWaechter>.Instance);
        return new PartieStartLimits(db, Options.Create(options), waechter, zeit, new SchachZaehler(messwerte.Factory));
    }

    private static SchachDbContext Kontext(string cs) =>
        new(new DbContextOptionsBuilder<SchachDbContext>().UseNpgsql(SchachConnectionString.Normalize(cs)).Options);

    private static Partie Partie(Guid? spielerId, PartieStatus status, DateTimeOffset erstelltAm) => new()
    {
        Id = Guid.NewGuid(),
        SpielerId = spielerId,
        GegnerId = "ilse",
        SpielerFarbe = Farbe.Weiss,
        Bedenkzeit = Bedenkzeit.OhneUhr,
        Status = status,
        Ergebnis = status == PartieStatus.Beendet ? Ergebnis.Remis : null,
        Grund = status == PartieStatus.Beendet ? Grund.Vereinbarung : null,
        BeendetAm = status == PartieStatus.Beendet ? erstelltAm : null,
        ZaehltFuerElo = spielerId is not null,
        Seed = 42,
        ErstelltAm = erstelltAm,
        LetzterZugAm = erstelltAm,
        Fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
    };
}
