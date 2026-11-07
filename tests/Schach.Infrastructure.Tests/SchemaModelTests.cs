using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Schach.Domain.Modell;
using Schach.Infrastructure.Persistence;

namespace Schach.Infrastructure.Tests;

/// <summary>Schema nach SCHACH-SPEC 8.12 auf Modell- und Migrationsebene, ohne Datenbank.</summary>
public sealed class SchemaModelTests : IDisposable
{
    private readonly SchachDbContext _db = new(new DbContextOptionsBuilder<SchachDbContext>()
        .UseNpgsql(SchachConnectionString.Normalize("Host=localhost;Database=unbenutzt"))
        .Options);

    public void Dispose() => _db.Dispose();

    private IEntityType Entity<T>() => _db.Model.FindEntityType(typeof(T))
        ?? throw new InvalidOperationException($"{typeof(T).Name} fehlt im Modell.");

    private IIndex Index<T>(params string[] spalten) =>
        Entity<T>().GetIndexes().SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(spalten))
        ?? throw new InvalidOperationException($"Index {typeof(T).Name}({string.Join(", ", spalten)}) fehlt.");

    [Theory]
    [InlineData(typeof(Partie), "Partie")]
    [InlineData(typeof(Zug), "Zug")]
    [InlineData(typeof(Spruch), "Spruch")]
    [InlineData(typeof(Bilanz), "Bilanz")]
    [InlineData(typeof(Spieler), "Spieler")]
    [InlineData(typeof(Anmeldung), "Anmeldung")]
    [InlineData(typeof(AnmeldungVersuch), "AnmeldungVersuch")]
    [InlineData(typeof(LlmVerbrauch), "LlmVerbrauch")]
    public void Entitaet_liegt_in_der_Tabelle_aus_der_Spec(Type typ, string tabelle)
    {
        _db.Model.FindEntityType(typ)!.GetTableName().Should().Be(tabelle);
    }

    [Fact]
    public void Zug_ist_eindeutig_je_Partie_und_Halbzug()
    {
        Entity<Zug>().FindPrimaryKey()!.Properties.Select(p => p.Name)
            .Should().Equal(nameof(Zug.PartieId), nameof(Zug.Halbzug));
    }

    [Fact]
    public void Hoechstens_eine_offene_Partie_je_Spieler()
    {
        var index = Index<Partie>(nameof(Partie.SpielerId));

        index.IsUnique.Should().BeTrue();
        index.GetFilter().Should().Be("\"Status\" = 'Offen'");
    }

    [Fact]
    public void Indizes_aus_der_Spec_sind_vorhanden()
    {
        Index<Partie>(nameof(Partie.Status), nameof(Partie.LetzterZugAm)).IsUnique.Should().BeFalse();
        Index<Spieler>(nameof(Spieler.Email)).IsUnique.Should().BeTrue();
        Index<Anmeldung>(nameof(Anmeldung.Email)).IsUnique.Should().BeFalse();
        Index<Anmeldung>(nameof(Anmeldung.ErstelltAm)).IsUnique.Should().BeFalse();
        Index<Anmeldung>(nameof(Anmeldung.TokenHash)).IsUnique.Should().BeTrue();
        Index<AnmeldungVersuch>(nameof(AnmeldungVersuch.EmailHash), nameof(AnmeldungVersuch.ErstelltAm))
            .IsUnique.Should().BeFalse();
        Index<AnmeldungVersuch>(nameof(AnmeldungVersuch.ErstelltAm)).IsUnique.Should().BeFalse();
    }

    [Fact]
    public void Kein_Unique_Index_auf_Spruch_PartieId_NachHalbzug()
    {
        var spruch = Entity<Spruch>();

        spruch.GetIndexes().Should().NotContain(i => i.IsUnique);
        spruch.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Equal(nameof(Spruch.Id));
    }

    [Fact]
    public void Partie_Version_ist_Concurrency_Token()
    {
        Entity<Partie>().FindProperty(nameof(Partie.Version))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(Partie), typeof(Spieler))]
    [InlineData(typeof(Bilanz), typeof(Spieler))]
    [InlineData(typeof(Zug), typeof(Partie))]
    [InlineData(typeof(Spruch), typeof(Partie))]
    public void Loeschen_kaskadiert(Type abhaengig, Type haupt)
    {
        var fremdschluessel = _db.Model.FindEntityType(abhaengig)!.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == haupt);

        fremdschluessel.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Theory]
    [InlineData(typeof(Partie), nameof(Partie.Status))]
    [InlineData(typeof(Partie), nameof(Partie.SpielerFarbe))]
    [InlineData(typeof(Partie), nameof(Partie.Bedenkzeit))]
    [InlineData(typeof(Partie), nameof(Partie.Ergebnis))]
    [InlineData(typeof(Partie), nameof(Partie.Grund))]
    [InlineData(typeof(Zug), nameof(Zug.Seite))]
    [InlineData(typeof(Zug), nameof(Zug.Engine))]
    [InlineData(typeof(Spruch), nameof(Spruch.Ereignis))]
    [InlineData(typeof(Spruch), nameof(Spruch.Quelle))]
    public void Enums_liegen_als_Text_in_Postgres(Type typ, string eigenschaft)
    {
        _db.Model.FindEntityType(typ)!.FindProperty(eigenschaft)!.GetColumnType().Should().Be("text");
    }

    [Theory]
    [InlineData(typeof(Partie))]
    [InlineData(typeof(Spieler))]
    [InlineData(typeof(Anmeldung))]
    [InlineData(typeof(AnmeldungVersuch))]
    public void Guid_Schluessel_setzt_die_Anwendung(Type typ)
    {
        // Sonst erzeugt EF sequenzielle Guids; Partie- und Anmeldung-Id sind aber Zufalls-Geheimnisse.
        _db.Model.FindEntityType(typ)!.FindProperty("Id")!.ValueGenerated.Should().Be(ValueGenerated.Never);
    }

    [Fact]
    public void Data_Protection_Schluessel_bleiben_im_Modell()
    {
        _db.Model.FindEntityType("Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey")
            .Should().NotBeNull();
    }

    [Fact]
    public void Einzige_Migration_ist_0001_Schema()
    {
        _db.Database.GetMigrations().Should().ContainSingle().Which.Should().EndWith("_0001_Schema");
    }

    [Fact]
    public void Migration_deckt_das_Modell_ab()
    {
        _db.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public void Migration_legt_den_gefilterten_Unique_Index_an()
    {
        var operationen = UpOperationen();

        operationen.OfType<CreateIndexOperation>().Should().ContainSingle(i =>
            i.Table == "Partie" && i.IsUnique && i.Filter == "\"Status\" = 'Offen'"
            && i.Columns.SequenceEqual(new[] { "SpielerId" }));
        operationen.OfType<CreateTableOperation>().Single(t => t.Name == "Zug")
            .PrimaryKey!.Columns.Should().Equal("PartieId", "Halbzug");
    }

    [Fact]
    public void Migration_legt_alle_Tabellen_an()
    {
        UpOperationen().OfType<CreateTableOperation>().Select(t => t.Name).Should().BeEquivalentTo(
            "Partie", "Zug", "Spruch", "Bilanz", "Spieler", "Anmeldung", "AnmeldungVersuch", "LlmVerbrauch",
            "DataProtectionKeys");
    }

    private IReadOnlyList<MigrationOperation> UpOperationen()
    {
        var assembly = _db.GetService<IMigrationsAssembly>();
        var (_, typ) = assembly.Migrations.Single();
        return assembly.CreateMigration(typ, _db.Database.ProviderName!).UpOperations;
    }
}
