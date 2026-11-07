using Npgsql;
using Testcontainers.PostgreSql;

namespace Schach.Tests.Shared;

/// <summary>
/// Ein gemeinsamer <c>postgres:17</c>-Testcontainer je Testassembly, gestartet beim ersten Zugriff
/// (nach GladiusEtPilum <c>tests/Gladius.Data.Tests/SharedPostgres.cs</c>). Isolation je Testklasse
/// ueber eine eigene, eindeutig benannte Datenbank im selben Container statt eines Containers je
/// Klasse. Der Container wird nie explizit entsorgt: Testcontainers' Ryuk entfernt ihn am Ende.
///
/// Wird als Quelldatei in Infrastructure.Tests und Api.Tests eingebunden. Nur aus Tests mit
/// <see cref="ContainerFactAttribute"/> aufrufen.
/// </summary>
internal static class SharedPostgres
{
    private static readonly SemaphoreSlim StartGate = new(1, 1);
    private static readonly SemaphoreSlim CreateDatabaseGate = new(1, 1);
    private static PostgreSqlContainer? _container;

    /// <summary>Startet (einmal) und liefert die Verbindung zur Wartungsdatenbank.</summary>
    public static async Task<string> AdminConnectionStringAsync()
    {
        if (Volatile.Read(ref _container) is { } running)
        {
            return running.GetConnectionString();
        }

        await StartGate.WaitAsync();
        try
        {
            if (_container is null)
            {
                var container = new PostgreSqlBuilder("postgres:17").Build();
                await container.StartAsync();
                Volatile.Write(ref _container, container);
            }
        }
        finally
        {
            StartGate.Release();
        }

        return _container!.GetConnectionString();
    }

    /// <summary>Legt eine frische, eindeutig benannte Datenbank an. Der Aufrufer loescht sie mit
    /// <see cref="DropDatabaseAsync"/>.</summary>
    public static async Task<string> CreateDatabaseAsync(string prefix)
    {
        var admin = await AdminConnectionStringAsync();
        var databaseName = $"{prefix}_{Guid.NewGuid():N}";

        // CREATE DATABASE klont template1, das Postgres bei parallelem Zugriff verweigert: serialisieren.
        await CreateDatabaseGate.WaitAsync();
        try
        {
            await using var connection = new NpgsqlConnection(admin);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            CreateDatabaseGate.Release();
        }

        return new NpgsqlConnectionStringBuilder(admin) { Database = databaseName }.ConnectionString;
    }

    /// <summary>Loescht eine mit <see cref="CreateDatabaseAsync"/> angelegte Datenbank.</summary>
    public static async Task DropDatabaseAsync(string connectionString)
    {
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var admin = await AdminConnectionStringAsync();

        using (var poolConnection = new NpgsqlConnection(connectionString))
        {
            NpgsqlConnection.ClearPool(poolConnection);
        }

        await using var connection = new NpgsqlConnection(admin);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
