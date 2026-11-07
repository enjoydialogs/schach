using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Schach.Infrastructure.Persistence;
using Schach.Tests.Shared;

namespace Schach.Infrastructure.Tests;

public sealed class SchachDbContextTests
{
    private static SchachDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<SchachDbContext>()
            .UseNpgsql(SchachConnectionString.Normalize(connectionString))
            .Options);

    [Fact]
    public void Modell_enthaelt_die_Data_Protection_Schluessel()
    {
        using var db = CreateContext("Host=localhost;Database=unbenutzt");

        db.Model.FindEntityType(typeof(DataProtectionKey)).Should().NotBeNull();
    }

    [ContainerFact]
    [Trait("Category", "Container")]
    public async Task MigrateAsync_laeuft_gegen_frisches_Postgres_17()
    {
        var connectionString = await SharedPostgres.CreateDatabaseAsync("schach_infra");
        try
        {
            await using var db = CreateContext(connectionString);

            await db.Database.MigrateAsync(TestContext.Current.CancellationToken);

            (await db.Database.CanConnectAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
            var result = await db.RunInTransactionAsync(_ => Task.FromResult(42), TestContext.Current.CancellationToken);
            result.Should().Be(42);
        }
        finally
        {
            await SharedPostgres.DropDatabaseAsync(connectionString);
        }
    }
}
