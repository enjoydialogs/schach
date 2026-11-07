using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Schach.Application.Abstractions;

namespace Schach.Infrastructure.Persistence;

/// <summary>
/// Der eine DbContext (PostgreSQL 17, EF Core 10). Phase 0a traegt nur die Data-Protection-
/// Schluessel (SCHACH-SPEC 9.1). Session 0b ergaenzt die Entitaeten aus SCHACH-SPEC 8.12 und
/// schreibt die einzige Startmigration <c>0001_Schema</c>, die auch die Schluesseltabelle anlegt.
/// Bis dahin ist <c>MigrateAsync</c> beim Start ein No-op.
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
