using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Schach.Application.Abstractions;
using Schach.Infrastructure.Persistence;
using Schach.Infrastructure.Time;

namespace Schach.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registriert alles aus <c>Schach.Infrastructure</c>. Phase 0a: DbContext, Data Protection,
    /// Uhr. Spaetere Sessions haengen ihren Block hier an (Email S2a, Engine S2b, Llm S2c).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = SchachConnectionString.Normalize(
            configuration.GetConnectionString(SchachConnectionString.Name));

        services.AddDbContext<SchachDbContext>(o =>
            o.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<SchachDbContext>());

        // SCHACH-SPEC 9.1: Schluesselring in Postgres, fester Anwendungsname.
        services.AddDataProtection()
            .PersistKeysToDbContext<SchachDbContext>()
            .SetApplicationName("Schach");

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IGameClock, SystemGameClock>();

        return services;
    }
}
