using Microsoft.EntityFrameworkCore;
using Npgsql;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Persistence;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests;

/// <summary>
/// Verhalten des Schemas aus <c>0001_Schema</c> gegen ein frisches Postgres 17 (SCHACH-SPEC 8.12,
/// 8.8). Nur in CI (<see cref="ContainerFactAttribute"/>). Jeder Test migriert eine eigene Datenbank.
/// </summary>
public sealed class SchemaPostgresTests
{
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Zweite_offene_Partie_eines_Spielers_ist_verboten() =>
        await MitDatenbankAsync(async cs =>
        {
            var spieler = NeuerSpieler();
            await using (var db = Kontext(cs))
            {
                db.Add(spieler);
                db.Add(NeuePartie(spieler.Id, PartieStatus.Offen));
                await db.SaveChangesAsync(Ct);
            }

            await using var db2 = Kontext(cs);
            db2.Add(NeuePartie(spieler.Id, PartieStatus.Offen));
            var act = () => db2.SaveChangesAsync(Ct);

            (await act.Should().ThrowAsync<DbUpdateException>())
                .WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Beendete_Partien_und_Gast_Partien_fallen_nicht_unter_den_Offen_Index() =>
        await MitDatenbankAsync(async cs =>
        {
            var spieler = NeuerSpieler();
            await using var db = Kontext(cs);
            db.Add(spieler);
            db.Add(NeuePartie(spieler.Id, PartieStatus.Offen));
            db.Add(NeuePartie(spieler.Id, PartieStatus.Beendet));
            db.Add(NeuePartie(spieler.Id, PartieStatus.Beendet));
            db.Add(NeuePartie(null, PartieStatus.Offen));
            db.Add(NeuePartie(null, PartieStatus.Offen));

            await db.SaveChangesAsync(Ct);

            (await db.Set<Partie>().CountAsync(Ct)).Should().Be(5);
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Derselbe_Halbzug_einer_Partie_ist_verboten() =>
        await MitDatenbankAsync(async cs =>
        {
            var partie = NeuePartie(null, PartieStatus.Offen);
            await using (var db = Kontext(cs))
            {
                db.Add(partie);
                db.Add(NeuerZug(partie.Id, 1));
                await db.SaveChangesAsync(Ct);
            }

            await using var db2 = Kontext(cs);
            db2.Add(NeuerZug(partie.Id, 1));
            var act = () => db2.SaveChangesAsync(Ct);

            (await act.Should().ThrowAsync<DbUpdateException>())
                .WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Zwei_Sprueche_nach_demselben_Halbzug_sind_erlaubt() =>
        await MitDatenbankAsync(async cs =>
        {
            var partie = NeuePartie(null, PartieStatus.Offen);
            await using var db = Kontext(cs);
            db.Add(partie);
            db.Add(NeuerSpruch(partie.Id, 4, SpruchEreignis.FigurGewonnen));
            db.Add(NeuerSpruch(partie.Id, 4, SpruchEreignis.SchachGegeben));

            await db.SaveChangesAsync(Ct);

            (await db.Set<Spruch>().CountAsync(s => s.PartieId == partie.Id, Ct)).Should().Be(2);
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Veraltete_Partie_Version_scheitert_mit_Concurrency_Fehler() =>
        await MitDatenbankAsync(async cs =>
        {
            var partie = NeuePartie(null, PartieStatus.Offen);
            await using (var db = Kontext(cs))
            {
                db.Add(partie);
                await db.SaveChangesAsync(Ct);
            }

            await using var tab1 = Kontext(cs);
            await using var tab2 = Kontext(cs);
            var p1 = await tab1.Set<Partie>().SingleAsync(p => p.Id == partie.Id, Ct);
            var p2 = await tab2.Set<Partie>().SingleAsync(p => p.Id == partie.Id, Ct);

            p1.Halbzuege = 1;
            p1.Version++;
            await tab1.SaveChangesAsync(Ct);

            p2.Halbzuege = 1;
            p2.Version++;
            var act = () => tab2.SaveChangesAsync(Ct);

            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Spieler_loeschen_kaskadiert_in_der_Datenbank() =>
        await MitDatenbankAsync(async cs =>
        {
            var spieler = NeuerSpieler();
            var fremd = NeuerSpieler();
            var partie = NeuePartie(spieler.Id, PartieStatus.Beendet);
            var fremdePartie = NeuePartie(fremd.Id, PartieStatus.Offen);
            await using (var db = Kontext(cs))
            {
                db.AddRange(spieler, fremd, partie, fremdePartie);
                db.AddRange(NeuerZug(partie.Id, 1), NeuerZug(partie.Id, 2), NeuerZug(fremdePartie.Id, 1));
                db.AddRange(NeuerSpruch(partie.Id, 0, SpruchEreignis.Partiebeginn), NeuerSpruch(fremdePartie.Id, 0, SpruchEreignis.Partiebeginn));
                db.AddRange(NeueBilanz(spieler.Id, "ilse"), NeueBilanz(spieler.Id, "bruno"), NeueBilanz(fremd.Id, "ilse"));
                await db.SaveChangesAsync(Ct);
            }

            // Roh per SQL, damit nur die Fremdschluessel der Datenbank kaskadieren, nicht EF im Speicher.
            await using (var db = Kontext(cs))
            {
                await db.Database.ExecuteSqlAsync($"""DELETE FROM "Spieler" WHERE "Id" = {spieler.Id}""", Ct);
            }

            await using var pruef = Kontext(cs);
            (await pruef.Set<Partie>().Select(p => p.Id).ToListAsync(Ct)).Should().Equal(fremdePartie.Id);
            (await pruef.Set<Zug>().Select(z => z.PartieId).ToListAsync(Ct)).Should().Equal(fremdePartie.Id);
            (await pruef.Set<Spruch>().Select(s => s.PartieId).ToListAsync(Ct)).Should().Equal(fremdePartie.Id);
            (await pruef.Set<Bilanz>().Select(b => b.SpielerId).ToListAsync(Ct)).Should().Equal(fremd.Id);
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Enums_stehen_als_Text_in_der_Datenbank() =>
        await MitDatenbankAsync(async cs =>
        {
            var partie = NeuePartie(null, PartieStatus.Offen);
            partie.Bedenkzeit = Bedenkzeit.Fischer5Plus3;
            await using var db = Kontext(cs);
            db.Add(partie);
            await db.SaveChangesAsync(Ct);

            var zeile = await db.Database
                .SqlQuery<string>($"""SELECT "Status" || '|' || "Bedenkzeit" || '|' || "SpielerFarbe" AS "Value" FROM "Partie" """)
                .SingleAsync(Ct);

            zeile.Should().Be("Offen|Fischer5Plus3|Weiss");
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Alle_Entitaeten_lassen_sich_speichern_und_laden() =>
        await MitDatenbankAsync(async cs =>
        {
            var spieler = NeuerSpieler();
            var partie = NeuePartie(spieler.Id, PartieStatus.Offen);
            var zug = NeuerZug(partie.Id, 1);
            zug.Engine = ZugEngine.Maia;
            zug.MaiaRang = 2;
            zug.PAnteil = 0.5;
            zug.W = 0.41;
            zug.Wdl = new Wdl { W = 0.41, D = 0.18, L = 0.41 };
            var monat = new DateOnly(2026, 11, 1);
            await using (var db = Kontext(cs))
            {
                db.AddRange(spieler, partie, zug, NeueBilanz(spieler.Id, "ilse"));
                db.Add(NeuerSpruch(partie.Id, 0, SpruchEreignis.Partiebeginn));
                db.Add(new Anmeldung
                {
                    Id = Guid.NewGuid(),
                    Email = "a@example.org",
                    TokenHash = new byte[32],
                    CodeHash = new byte[32],
                    Fehlversuche = 0,
                    ErstelltAm = Jetzt,
                    LaeuftAbAm = Jetzt.AddMinutes(15),
                });
                db.Add(new AnmeldungVersuch { Id = Guid.NewGuid(), EmailHash = new byte[32], ErstelltAm = Jetzt });
                db.Add(new LlmVerbrauch { Monat = monat, PromptTokens = 10, Aufrufe = 1 });
                await db.SaveChangesAsync(Ct);
            }

            await using var pruef = Kontext(cs);
            var geladen = await pruef.Set<Zug>().SingleAsync(Ct);
            geladen.Wdl.Should().BeEquivalentTo(new Wdl { W = 0.41, D = 0.18, L = 0.41 });
            geladen.Engine.Should().Be(ZugEngine.Maia);
            (await pruef.Set<Spieler>().SingleAsync(Ct)).Email.Should().Be(spieler.Email);
            (await pruef.Set<Anmeldung>().CountAsync(Ct)).Should().Be(1);
            (await pruef.Set<AnmeldungVersuch>().CountAsync(Ct)).Should().Be(1);
            (await pruef.Set<LlmVerbrauch>().SingleAsync(l => l.Monat == monat, Ct)).PromptTokens.Should().Be(10);
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Gleicher_TokenHash_und_gleiche_Email_sind_verboten() =>
        await MitDatenbankAsync(async cs =>
        {
            await using (var db = Kontext(cs))
            {
                db.Add(NeuerSpieler("doppelt@example.org"));
                await db.SaveChangesAsync(Ct);
            }

            await using var db2 = Kontext(cs);
            db2.Add(NeuerSpieler("doppelt@example.org"));
            var act = () => db2.SaveChangesAsync(Ct);

            (await act.Should().ThrowAsync<DbUpdateException>())
                .WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        });

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Indizes_aus_der_Spec_existieren() =>
        await MitDatenbankAsync(async cs =>
        {
            await using var db = Kontext(cs);
            var indizes = await db.Database
                .SqlQuery<string>($"""SELECT indexdef AS "Value" FROM pg_indexes WHERE schemaname = 'public'""")
                .ToListAsync(Ct);

            indizes.Should().Contain(i => i.Contains("UNIQUE", StringComparison.Ordinal)
                && i.Contains("\"Partie\"", StringComparison.Ordinal)
                && i.Contains("(\"SpielerId\")", StringComparison.Ordinal)
                && i.Contains("WHERE", StringComparison.Ordinal));
            indizes.Should().Contain(i => i.Contains("ON public.\"Partie\"", StringComparison.Ordinal) && i.Contains("(\"Status\", \"LetzterZugAm\")", StringComparison.Ordinal));
            indizes.Should().Contain(i => i.Contains("UNIQUE", StringComparison.Ordinal) && i.Contains("ON public.\"Anmeldung\"", StringComparison.Ordinal) && i.Contains("(\"TokenHash\")", StringComparison.Ordinal));
            indizes.Should().Contain(i => i.Contains("ON public.\"Anmeldung\"", StringComparison.Ordinal) && i.Contains("(\"Email\")", StringComparison.Ordinal));
            indizes.Should().Contain(i => i.Contains("ON public.\"Anmeldung\"", StringComparison.Ordinal) && i.Contains("(\"ErstelltAm\")", StringComparison.Ordinal));
            indizes.Should().Contain(i => i.Contains("ON public.\"AnmeldungVersuch\"", StringComparison.Ordinal) && i.Contains("(\"EmailHash\", \"ErstelltAm\")", StringComparison.Ordinal));
            indizes.Should().Contain(i => i.Contains("ON public.\"AnmeldungVersuch\"", StringComparison.Ordinal) && i.Contains("(\"ErstelltAm\")", StringComparison.Ordinal));
            indizes.Should().NotContain(i => i.Contains("UNIQUE", StringComparison.Ordinal) && i.Contains("ON public.\"Spruch\"", StringComparison.Ordinal) && i.Contains("\"NachHalbzug\"", StringComparison.Ordinal));
            indizes.Should().Contain(i => i.Contains("ON public.\"DataProtectionKeys\"", StringComparison.Ordinal));
        });

    private static SchachDbContext Kontext(string connectionString) =>
        new(new DbContextOptionsBuilder<SchachDbContext>()
            .UseNpgsql(SchachConnectionString.Normalize(connectionString))
            .Options);

    private static async Task MitDatenbankAsync(Func<string, Task> test)
    {
        var cs = await SharedPostgres.CreateDatabaseAsync("schach_schema");
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

    private static Spieler NeuerSpieler(string? email = null) => new()
    {
        Id = Guid.NewGuid(),
        Email = email ?? $"{Guid.NewGuid():N}@example.org",
        ErstelltAm = Jetzt,
        Sicherheitsstempel = Guid.NewGuid(),
        Elo = 800,
    };

    private static Partie NeuePartie(Guid? spielerId, PartieStatus status) => new()
    {
        Id = Guid.NewGuid(),
        SpielerId = spielerId,
        GegnerId = "ilse",
        SpielerFarbe = Farbe.Weiss,
        Bedenkzeit = Bedenkzeit.OhneUhr,
        Status = status,
        Ergebnis = status == PartieStatus.Beendet ? Ergebnis.Remis : null,
        Grund = status == PartieStatus.Beendet ? Grund.Vereinbarung : null,
        ErstelltAm = Jetzt,
        LetzterZugAm = Jetzt,
        BeendetAm = status == PartieStatus.Beendet ? Jetzt : null,
        Halbzuege = 0,
        Fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
        ZaehltFuerElo = spielerId is not null,
        Seed = 42,
    };

    private static Zug NeuerZug(Guid partieId, int halbzug) => new()
    {
        PartieId = partieId,
        Halbzug = halbzug,
        Seite = halbzug % 2 == 1 ? Farbe.Weiss : Farbe.Schwarz,
        Uci = "e2e4",
        San = "e4",
        FenDanach = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
        GespieltAm = Jetzt,
    };

    private static Spruch NeuerSpruch(Guid partieId, int nachHalbzug, SpruchEreignis ereignis) => new()
    {
        PartieId = partieId,
        NachHalbzug = nachHalbzug,
        Ereignis = ereignis,
        Text = "Na dann.",
        Quelle = SpruchQuelle.Ersatz,
    };

    private static Bilanz NeueBilanz(Guid spielerId, string gegnerId) => new()
    {
        SpielerId = spielerId,
        GegnerId = gegnerId,
        Siege = 1,
    };
}
