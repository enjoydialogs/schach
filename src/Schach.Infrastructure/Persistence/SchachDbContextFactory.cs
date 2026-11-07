using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Schach.Infrastructure.Persistence;

/// <summary>
/// Design-Time-Factory fuer <c>dotnet ef</c>, nie zur Laufzeit benutzt. Die Verbindung kommt aus
/// <c>ConnectionStrings__Schach</c> und hat keinen Standardwert: sonst migrierte jeder Checkout
/// unbemerkt gegen dieselbe Datenbank.
/// </summary>
public sealed class SchachDbContextFactory : IDesignTimeDbContextFactory<SchachDbContext>
{
    public SchachDbContext CreateDbContext(string[] args)
    {
        var connectionString = SchachConnectionString.Normalize(
            Environment.GetEnvironmentVariable(SchachConnectionString.Variable));
        var options = new DbContextOptionsBuilder<SchachDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new SchachDbContext(options);
    }
}
