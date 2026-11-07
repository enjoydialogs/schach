using Microsoft.EntityFrameworkCore;
using Npgsql;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Persistence;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests;

/// <summary>
/// Faelle zu <c>0001_Schema</c>, die <see cref="SchemaPostgresTests"/> nicht abdeckt: die
/// 32-Byte-Check-Constraints der Hashspalten und Gast-Partien beim Loeschen eines Spielers
/// (SCHACH-SPEC 8.12). Nur in CI (<see cref="ContainerFactAttribute"/>).
/// </summary>
public sealed class SchemaPostgresZusatzTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Anmeldung_Hashes_verlangen_genau_32_Byte() =>
        await MitDatenbankAsync(async cs =>
        {
            await SpeichernAsync(cs, NeueAnmeldung(32, 32));

            await SollCheckVerletzenAsync(cs, NeueAnmeldung(31, 32));
            await SollCheckVerletzenAsync(cs, NeueAnmeldung(33, 32));
            await SollCheckVerletzenAsync(cs, NeueAnmeldung(32, 31));
            await SollCheckVerletzenAsync(cs, NeueAnmeldung(32, 33));
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task AnmeldungVersuch_EmailHash_verlangt_genau_32_Byte() =>
        await MitDatenbankAsync(async cs =>
        {
            await SpeichernAsync(cs, NeuerVersuch(32));

            await SollCheckVerletzenAsync(cs, NeuerVersuch(31));
            await SollCheckVerletzenAsync(cs, NeuerVersuch(33));
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Spieler_loeschen_laesst_Gast_Partien_stehen() =>
        await MitDatenbankAsync(async cs =>
        {
            var spieler = NeuerSpieler();
            var eigene = NeuePartie(spieler.Id);
            var gast = NeuePartie(null);
            await using (var db = Kontext(cs))
            {
                db.AddRange(spieler, eigene, gast);
                db.AddRange(NeuerZug(eigene.Id, 1), NeuerZug(gast.Id, 1));
                await db.SaveChangesAsync(Ct);
            }

            // Roh per SQL, damit nur die Fremdschluessel der Datenbank kaskadieren.
            await using (var db = Kontext(cs))
            {
                await db.Database.ExecuteSqlAsync($"""DELETE FROM "Spieler" WHERE "Id" = {spieler.Id}""", Ct);
            }

            await using var pruef = Kontext(cs);
            (await pruef.Set<Partie>().CountAsync(p => p.SpielerId == spieler.Id, Ct)).Should().Be(0);
            (await pruef.Set<Zug>().CountAsync(z => z.PartieId == eigene.Id, Ct)).Should().Be(0);
            (await pruef.Set<Partie>().Select(p => p.Id).ToListAsync(Ct)).Should().Equal(gast.Id);
            (await pruef.Set<Zug>().Select(z => z.PartieId).ToListAsync(Ct)).Should().Equal(gast.Id);
        });

    private static SchachDbContext Kontext(string connectionString) =>
        new(new DbContextOptionsBuilder<SchachDbContext>()
            .UseNpgsql(SchachConnectionString.Normalize(connectionString))
            .Options);

    private static async Task MitDatenbankAsync(Func<string, Task> test)
    {
        var cs = await SharedPostgres.CreateDatabaseAsync("schach_schema_zusatz");
        try
        {
            await using (var db = Kontext(cs))
            {
                await db.Database.MigrateAsync(Ct);
            }

            await test(cs);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(cs);
        }
    }

    private static async Task SpeichernAsync(string cs, object zeile)
    {
        await using var db = Kontext(cs);
        db.Add(zeile);
        await db.SaveChangesAsync(Ct);
    }

    private static async Task SollCheckVerletzenAsync(string cs, object zeile)
    {
        await using var db = Kontext(cs);
        db.Add(zeile);
        var act = () => db.SaveChangesAsync(Ct);

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    // Zufaelliger Inhalt, damit nur der Check-Constraint greift, nie der Unique-Index auf TokenHash.
    private static byte[] Hash(int laenge)
    {
        var hash = new byte[laenge];
        Random.Shared.NextBytes(hash);
        return hash;
    }

    private static Anmeldung NeueAnmeldung(int tokenHashLaenge, int codeHashLaenge) => new()
    {
        Id = Guid.NewGuid(),
        Email = "a@example.org",
        TokenHash = Hash(tokenHashLaenge),
        CodeHash = Hash(codeHashLaenge),
        ErstelltAm = Jetzt,
        LaeuftAbAm = Jetzt.AddMinutes(15),
    };

    private static AnmeldungVersuch NeuerVersuch(int emailHashLaenge) => new()
    {
        Id = Guid.NewGuid(),
        EmailHash = Hash(emailHashLaenge),
        ErstelltAm = Jetzt,
    };

    private static Spieler NeuerSpieler() => new()
    {
        Id = Guid.NewGuid(),
        Email = $"{Guid.NewGuid():N}@example.org",
        ErstelltAm = Jetzt,
        Sicherheitsstempel = Guid.NewGuid(),
        Elo = 800,
    };

    private static Partie NeuePartie(Guid? spielerId) => new()
    {
        Id = Guid.NewGuid(),
        SpielerId = spielerId,
        GegnerId = "ilse",
        SpielerFarbe = Farbe.Weiss,
        Bedenkzeit = Bedenkzeit.OhneUhr,
        Status = PartieStatus.Offen,
        ErstelltAm = Jetzt,
        LetzterZugAm = Jetzt,
        Halbzuege = 0,
        Fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
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
        FenDanach = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
        GespieltAm = Jetzt,
    };
}
