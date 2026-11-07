using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Identity;
using Schach.Infrastructure.Persistence;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests.Identity;

/// <summary>Sweep-Block D (SCHACH-SPEC 8.7): Anmeldung nach 1 h, AnmeldungVersuch nach 24 h weg.</summary>
public sealed class AnmeldungenAufraeumenTests
{
    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Loescht_nur_Zeilen_jenseits_der_Fristen()
    {
        var ct = TestContext.Current.CancellationToken;
        var jetzt = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var connectionString = await SharedPostgres.CreateDatabaseAsync("schach_sweep");
        try
        {
            await using var db = new SchachDbContext(new DbContextOptionsBuilder<SchachDbContext>()
                .UseNpgsql(SchachConnectionString.Normalize(connectionString)).Options);
            await db.Database.MigrateAsync(ct);

            Anmeldung Anmeldung(TimeSpan alter) => new()
            {
                Id = Guid.NewGuid(),
                Email = $"{Guid.NewGuid():N}@example.test",
                TokenHash = MagicLinkKrypto.TokenHash(MagicLinkKrypto.NeuesToken())!,
                CodeHash = MagicLinkKrypto.CodeHash(Guid.NewGuid(), "123456"),
                ErstelltAm = jetzt - alter,
                LaeuftAbAm = jetzt - alter + TimeSpan.FromMinutes(15),
            };
            AnmeldungVersuch Versuch(TimeSpan alter) => new()
            {
                Id = Guid.NewGuid(),
                EmailHash = EmailAdresse.ZaehlHash("a@example.test"),
                ErstelltAm = jetzt - alter,
            };

            var bleibt = Anmeldung(TimeSpan.FromMinutes(59));
            var bleibtVersuch = Versuch(TimeSpan.FromHours(23));
            db.AddRange(bleibt, Anmeldung(TimeSpan.FromMinutes(61)), bleibtVersuch, Versuch(TimeSpan.FromHours(25)), Versuch(TimeSpan.FromHours(2)));
            await db.SaveChangesAsync(ct);

            var (anmeldungen, versuche) = await new AnmeldungenAufraeumen(db, new FakeTimeProvider(jetzt)).AusfuehrenAsync(ct);

            anmeldungen.Should().Be(1);
            versuche.Should().Be(1);
            (await db.Set<Anmeldung>().Select(a => a.Id).ToListAsync(ct)).Should().Equal(bleibt.Id);
            (await db.Set<AnmeldungVersuch>().CountAsync(ct)).Should().Be(2);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(connectionString);
        }
    }
}
