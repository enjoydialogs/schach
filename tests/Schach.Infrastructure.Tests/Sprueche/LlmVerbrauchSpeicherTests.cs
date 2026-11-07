using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Llm;
using Schach.Infrastructure.Llm;
using Schach.Infrastructure.Persistence;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests.Sprueche;

/// <summary>SCHACH-SPEC 11.1: <c>LlmVerbrauch</c> wird atomar hochgezaehlt. Nur in CI.</summary>
public sealed class LlmVerbrauchSpeicherTests
{
    private static readonly DateOnly November = new(2026, 11, 1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Parallele_Buchungen_gehen_nicht_verloren()
    {
        var cs = await SharedPostgres.CreateDatabaseAsync("schach_llm");
        try
        {
            await using var services = Dienste(cs);
            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<SchachDbContext>().Database.MigrateAsync(Ct);
            }

            var speicher = new LlmVerbrauchSpeicher(services.GetRequiredService<IServiceScopeFactory>());

            await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
                speicher.BucheAsync(November, new LlmBuchung(400, 30, 1, 0, 0), Ct)));
            await speicher.BucheAsync(November, new LlmBuchung(600, 0, 1, 1, 0), Ct);
            await speicher.BucheAsync(November, new LlmBuchung(0, 0, 1, 0, 1), Ct);

            (await speicher.LeseAsync(November, Ct)).Should().Be(new LlmVerbrauchStand(20 * 400 + 600, 20 * 30));
            (await speicher.LeseAsync(new DateOnly(2026, 10, 1), Ct)).Should().Be(new LlmVerbrauchStand(0, 0));

            await using var pruef = services.CreateAsyncScope();
            var zeile = await pruef.ServiceProvider.GetRequiredService<SchachDbContext>()
                .Set<Domain.Entitaeten.LlmVerbrauch>().SingleAsync(Ct);
            zeile.Aufrufe.Should().Be(22);
            zeile.Zeitueberschreitungen.Should().Be(1);
            zeile.Fehler.Should().Be(1);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(cs);
        }
    }

    private static ServiceProvider Dienste(string cs) =>
        new ServiceCollection()
            .AddDbContext<SchachDbContext>(o => o.UseNpgsql(SchachConnectionString.Normalize(cs)))
            .BuildServiceProvider();
}
