using Microsoft.EntityFrameworkCore;
using Npgsql;
using Schach.Domain;
using Schach.Domain.Modell;
using Schach.Infrastructure.Persistence;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests;

/// <summary>Die Migration <c>0001_Schema</c> gegen echtes Postgres 17 (nur CI).</summary>
public sealed class SchemaPostgresTests
{
    private const string UniqueVerletzt = "23505";

    private static SchachDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<SchachDbContext>()
            .UseNpgsql(SchachConnectionString.Normalize(connectionString))
            .Options);

    private static Spieler NeuerSpieler() => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@example.org",
        ErstelltAm = DateTimeOffset.UtcNow,
        Sicherheitsstempel = Guid.NewGuid(),
        Elo = 800,
    };

    private static Partie NeuePartie(Guid? spielerId, PartieStatus status = PartieStatus.Offen) => new()
    {
        Id = Guid.NewGuid(),
        SpielerId = spielerId,
        GegnerId = "testgegner",
        SpielerFarbe = Farbe.Weiss,
        Bedenkzeit = Bedenkzeit.OhneUhr,
        Status = status,
        ErstelltAm = DateTimeOffset.UtcNow,
        LetzterZugAm = DateTimeOffset.UtcNow,
        Fen = SchachDomain.GrundstellungFen,
        ZaehltFuerElo = spielerId is not null,
        Seed = 42,
    };

    private static Zug NeuerZug(Guid partieId, int halbzug) => new()
    {
        PartieId = partieId,
        Halbzug = halbzug,
        Seite = Farbe.Weiss,
        Uci = "e2e4",
        San = "e4",
        FenDanach = SchachDomain.GrundstellungFen,
        GespieltAm = DateTimeOffset.UtcNow,
    };

    private static async Task SollUniqueVerletzen(SchachDbContext db, CancellationToken ct)
    {
        var act = () => db.SaveChangesAsync(ct);
        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(UniqueVerletzt);
        db.ChangeTracker.Clear();
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Indizes_und_Kaskade_wirken_in_Postgres()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await SharedPostgres.CreateDatabaseAsync("schach_schema");
        try
        {
            await using var db = CreateContext(connectionString);
            await db.Database.MigrateAsync(ct);

            var spieler = NeuerSpieler();
            var offen = NeuePartie(spieler.Id);
            db.Add(spieler);
            db.Add(offen);
            await db.SaveChangesAsync(ct);

            // Gefiltert unique: eine zweite offene Partie desselben Spielers scheitert ...
            db.Add(NeuePartie(spieler.Id));
            await SollUniqueVerletzen(db, ct);

            // ... beendete Partien und offene Gast-Partien (SpielerId NULL) nicht.
            db.Add(NeuePartie(spieler.Id, PartieStatus.Beendet));
            db.Add(NeuePartie(spieler.Id, PartieStatus.Beendet));
            db.Add(NeuePartie(null));
            db.Add(NeuePartie(null));
            await db.SaveChangesAsync(ct);

            // Unique Zug(PartieId, Halbzug).
            db.Add(NeuerZug(offen.Id, 1));
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            db.Add(NeuerZug(offen.Id, 1));
            await SollUniqueVerletzen(db, ct);

            // Kein Unique-Index auf Spruch(PartieId, NachHalbzug).
            db.Add(new Spruch { PartieId = offen.Id, NachHalbzug = 1, Ereignis = SpruchEreignis.Zug, Text = "a", Quelle = SpruchQuelle.Ersatz });
            db.Add(new Spruch { PartieId = offen.Id, NachHalbzug = 1, Ereignis = SpruchEreignis.Sieg, Text = "b", Quelle = SpruchQuelle.Mistral });
            db.Add(new Bilanz { SpielerId = spieler.Id, GegnerId = "testgegner", Siege = 1 });
            await db.SaveChangesAsync(ct);

            // Enums liegen als Text in Postgres.
            var status = await db.Database
                .SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"Partie\" WHERE \"Id\" = {offen.Id}")
                .SingleAsync(ct);
            status.Should().Be("Offen");

            // Konto loeschen kaskadiert auf alle abhaengigen Zeilen (SCHACH-SPEC 8.12, 9.7).
            db.ChangeTracker.Clear();
            await db.Set<Spieler>().Where(s => s.Id == spieler.Id).ExecuteDeleteAsync(ct);

            (await db.Set<Partie>().CountAsync(p => p.SpielerId == spieler.Id, ct)).Should().Be(0);
            (await db.Set<Zug>().CountAsync(z => z.PartieId == offen.Id, ct)).Should().Be(0);
            (await db.Set<Spruch>().CountAsync(s => s.PartieId == offen.Id, ct)).Should().Be(0);
            (await db.Set<Bilanz>().CountAsync(b => b.SpielerId == spieler.Id, ct)).Should().Be(0);
            (await db.Set<Partie>().CountAsync(p => p.SpielerId == null, ct)).Should().Be(2);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(connectionString);
        }
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Version_meldet_konkurrierende_Aenderung()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await SharedPostgres.CreateDatabaseAsync("schach_version");
        try
        {
            await using (var setup = CreateContext(connectionString))
            {
                await setup.Database.MigrateAsync(ct);
                setup.Add(NeuePartie(null));
                await setup.SaveChangesAsync(ct);
            }

            await using var erster = CreateContext(connectionString);
            await using var zweiter = CreateContext(connectionString);
            var a = await erster.Set<Partie>().SingleAsync(ct);
            var b = await zweiter.Set<Partie>().SingleAsync(ct);

            a.Halbzuege = 1;
            a.Version++;
            await erster.SaveChangesAsync(ct);

            b.Halbzuege = 1;
            b.Version++;
            var act = () => zweiter.SaveChangesAsync(ct);
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(connectionString);
        }
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Hashspalten_verlangen_32_Byte()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await SharedPostgres.CreateDatabaseAsync("schach_hash");
        try
        {
            await using var db = CreateContext(connectionString);
            await db.Database.MigrateAsync(ct);

            db.Add(new AnmeldungVersuch { Id = Guid.NewGuid(), EmailHash = new byte[32], ErstelltAm = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(ct);

            db.Add(new AnmeldungVersuch { Id = Guid.NewGuid(), EmailHash = new byte[31], ErstelltAm = DateTimeOffset.UtcNow });
            var act = () => db.SaveChangesAsync(ct);
            (await act.Should().ThrowAsync<DbUpdateException>())
                .WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be("23514");
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(connectionString);
        }
    }
}
