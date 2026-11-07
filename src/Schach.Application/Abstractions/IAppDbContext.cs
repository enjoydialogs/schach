using Microsoft.EntityFrameworkCore;

namespace Schach.Application.Abstractions;

/// <summary>
/// Datenzugriff der Use Cases. Bewusst generisch ueber <see cref="Set{TEntity}"/>, damit neue
/// Entitaeten (Session 0b und spaeter) diese eingefrorene Datei nicht aendern muessen.
/// Umgesetzt von <c>SchachDbContext</c> in <c>Schach.Infrastructure</c>.
/// </summary>
public interface IAppDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fuehrt <paramref name="arbeit"/> in genau einer Transaktion aus, innerhalb der
    /// Ausfuehrungsstrategie des Providers. Eine eigene Transaktion laeuft nur hierueber, nie ueber
    /// ein rohes <c>Database.BeginTransactionAsync()</c> (das scheitert mit Wiederholungsstrategie).
    /// T1 und T2 eines Zugs (SCHACH-SPEC 8.8) sind je ein Aufruf.
    /// </summary>
    Task<T> RunInTransactionAsync<T>(Func<CancellationToken, Task<T>> arbeit, CancellationToken cancellationToken = default);
}
