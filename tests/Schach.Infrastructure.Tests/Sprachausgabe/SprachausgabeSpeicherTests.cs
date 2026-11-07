using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schach.Application.Sprachausgabe;
using Schach.Infrastructure.Persistence;
using Schach.Infrastructure.Sprachausgabe;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests.Sprachausgabe;

/// <summary>SCHACH-SPEC 6.9, 8.12: Ton-Cache und Zeichen-Zaehlwerk in Postgres. Nur in CI.</summary>
public sealed class SprachausgabeSpeicherTests
{
    private static readonly DateOnly November = new(2026, 11, 1);
    private static readonly DateTimeOffset Jetzt = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Cache_liest_frischt_auf_und_raeumt_nach_Grenze_auf()
    {
        var cs = await SharedPostgres.CreateDatabaseAsync("schach_tts_cache");
        try
        {
            await using var services = await DiensteAsync(cs);
            var cache = new SpruchStimmeSpeicher(services.GetRequiredService<IServiceScopeFactory>());
            var alt = SprachausgabeDienst.Hash("m", "v", "Alt");
            var neu = SprachausgabeDienst.Hash("m", "v", "Neu");

            await cache.SpeichereAsync(alt, [1, 2, 3], 3, Jetzt, Ct);
            await cache.SpeichereAsync(alt, [9], 3, Jetzt, Ct);
            await cache.SpeichereAsync(neu, [4], 3, Jetzt, Ct);
            (await cache.LeseAsync(neu, Jetzt.AddDays(80), Ct)).Should().Equal(4);

            (await cache.LeseAsync(alt, Jetzt, Ct)).Should().Equal(1, 2, 3);
            (await cache.LeseAsync(SprachausgabeDienst.Hash("m", "v", "Fehlt"), Jetzt, Ct)).Should().BeNull();

            (await cache.AufraeumenAsync(Jetzt.AddDays(10), Ct)).Should().Be(1);
            (await cache.LeseAsync(alt, Jetzt, Ct)).Should().BeNull();
            (await cache.LeseAsync(neu, Jetzt, Ct)).Should().Equal(4);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(cs);
        }
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task Parallele_Zeichenbuchungen_gehen_nicht_verloren()
    {
        var cs = await SharedPostgres.CreateDatabaseAsync("schach_tts_verbrauch");
        try
        {
            await using var services = await DiensteAsync(cs);
            var speicher = new SprachVerbrauchSpeicher(services.GetRequiredService<IServiceScopeFactory>());

            await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => speicher.BucheAsync(November, new SprachBuchung(90, 1, 0, 0), Ct)));
            await speicher.BucheAsync(November, new SprachBuchung(0, 1, 0, 1), Ct);

            (await speicher.LeseZeichenAsync(November, Ct)).Should().Be(1800);
            (await speicher.LeseZeichenAsync(new DateOnly(2026, 10, 1), Ct)).Should().Be(0);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(cs);
        }
    }

    private static async Task<ServiceProvider> DiensteAsync(string cs)
    {
        var services = new ServiceCollection()
            .AddDbContext<SchachDbContext>(o => o.UseNpgsql(SchachConnectionString.Normalize(cs)))
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SchachDbContext>().Database.MigrateAsync(Ct);
        return services;
    }
}
