using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Schach.Domain.Entitaeten;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Tests;

/// <summary>
/// Schema nach SCHACH-SPEC 8.12 ohne Datenbank: Migrationsliste, Snapshot-Abgleich (nach Kronrat
/// <c>MigrationSnapshotBackstopTests</c>) und Modell-Eigenschaften, die ein Verhaltenstest gegen
/// Postgres nur in CI pruefen kann. Der Kontext wird nie geoeffnet.
/// </summary>
public sealed class SchemaModellTests
{
    private static SchachDbContext Kontext() =>
        new(new DbContextOptionsBuilder<SchachDbContext>()
            .UseNpgsql("Host=nur-modellvergleich.invalid;Database=keine")
            .Options);

    [Fact]
    public void Es_gibt_genau_die_eine_Migration_0001_Schema()
    {
        using var db = Kontext();

        db.Database.GetMigrations().Should().Equal("0001_Schema");
    }

    [Fact]
    public void Der_Snapshot_entspricht_dem_aktuellen_Modell()
    {
        using var db = Kontext();
        var services = ((IInfrastructure<IServiceProvider>)db).Instance;

        var snapshot = Laufzeitmodell(services, SnapshotModell(services));
        var aktuell = services.GetRequiredService<IDesignTimeModel>().Model;

        services.GetRequiredService<IMigrationsModelDiffer>()
            .HasDifferences(snapshot.GetRelationalModel(), aktuell.GetRelationalModel())
            .Should().BeFalse("das Modell hat Aenderungen ohne Migration (SCHACH-SPEC 15.1 Regel 6)");
    }

    [Fact]
    public void Das_Zielmodell_der_letzten_Migration_entspricht_dem_Snapshot()
    {
        using var db = Kontext();
        var services = ((IInfrastructure<IServiceProvider>)db).Instance;
        var assembly = services.GetRequiredService<IMigrationsAssembly>();
        var letzteId = assembly.Migrations.Keys.Order(StringComparer.Ordinal).Last();
        var letzte = assembly.CreateMigration(
            assembly.Migrations[letzteId], services.GetRequiredService<IDatabaseProvider>().Name);

        var ziel = Laufzeitmodell(services, letzte.TargetModel!);
        var snapshot = Laufzeitmodell(services, SnapshotModell(services));

        services.GetRequiredService<IMigrationsModelDiffer>()
            .HasDifferences(ziel.GetRelationalModel(), snapshot.GetRelationalModel())
            .Should().BeFalse($"das .Designer.cs von {letzteId} weicht vom Snapshot ab");
    }

    [Fact]
    public void Partie_Version_ist_Concurrency_Token()
    {
        using var db = Kontext();

        db.Model.FindEntityType(typeof(Partie))!.FindProperty(nameof(Partie.Version))!
            .IsConcurrencyToken.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(Partie), "Partie")]
    [InlineData(typeof(Zug), "Zug")]
    [InlineData(typeof(Spruch), "Spruch")]
    [InlineData(typeof(Bilanz), "Bilanz")]
    [InlineData(typeof(Spieler), "Spieler")]
    [InlineData(typeof(Anmeldung), "Anmeldung")]
    [InlineData(typeof(AnmeldungVersuch), "AnmeldungVersuch")]
    [InlineData(typeof(LlmVerbrauch), "LlmVerbrauch")]
    public void Tabellennamen_folgen_der_Spec(Type entitaet, string tabelle)
    {
        using var db = Kontext();

        db.Model.FindEntityType(entitaet)!.GetTableName().Should().Be(tabelle);
    }

    private static IModel SnapshotModell(IServiceProvider services) =>
        services.GetRequiredService<IMigrationsAssembly>().ModelSnapshot?.Model
            ?? throw new InvalidOperationException("Die Migrations-Assembly hat keinen Snapshot.");

    private static IModel Laufzeitmodell(IServiceProvider services, IModel model)
    {
        if (model is IMutableModel mutable)
        {
            model = mutable.FinalizeModel();
        }

        return services.GetRequiredService<IModelRuntimeInitializer>().Initialize(model);
    }
}
