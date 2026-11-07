using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Persistence;

/// <summary>
/// Der eine DbContext (PostgreSQL 17, EF Core 10): Data-Protection-Schluessel (SCHACH-SPEC 9.1)
/// und die Entitaeten aus SCHACH-SPEC 8.12 (<c>Schach.Domain.Entitaeten</c>, konfiguriert unter
/// <c>Persistence/Konfiguration</c>). Die Startmigration <c>0001_Schema</c> legt alles an; jede
/// weitere Aenderung ist eine eigene Vorwaertsmigration (SCHACH-SPEC 15.1 Regel 6).
/// Use Cases greifen ueber <see cref="IAppDbContext.Set{TEntity}"/> zu.
/// </summary>
public sealed class SchachDbContext(DbContextOptions<SchachDbContext> options)
    : DbContext(options), IAppDbContext, IDataProtectionKeyContext
{
    /// <summary>Data-Protection-Schluesselring (<c>PersistKeysToDbContext</c>).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public async Task<T> RunInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> arbeit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arbeit);
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                await using var transaction = await Database.BeginTransactionAsync(ct);
                var ergebnis = await arbeit(ct);
                await transaction.CommitAsync(ct);
                return ergebnis;
            },
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<DataProtectionKey>();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SchachDbContext).Assembly);
    }
}
