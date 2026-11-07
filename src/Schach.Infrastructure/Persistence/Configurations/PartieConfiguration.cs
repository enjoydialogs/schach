using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Modell;

namespace Schach.Infrastructure.Persistence.Configurations;

/// <summary><c>Partie</c> nach SCHACH-SPEC 8.12.</summary>
internal sealed class PartieConfiguration : IEntityTypeConfiguration<Partie>
{
    /// <summary>Filter des Index „hoechstens eine offene Partie je Spieler“ (SCHACH-SPEC 3.1).
    /// Gast-Partien (<c>SpielerId IS NULL</c>) fallen unter NULLS DISTINCT nicht darunter.</summary>
    public const string OffenFilter = "\"Status\" = 'Offen'";

    public void Configure(EntityTypeBuilder<Partie> builder)
    {
        builder.ToTable("Partie");
        builder.HasKey(p => p.Id);
        // Zufalls-Id v4 aus der Anwendung, keine sequenzielle Guid aus EF (SCHACH-SPEC 8.3).
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.GegnerId).IsRequired();
        builder.Property(p => p.SpielerFarbe).HasConversion<string>();
        builder.Property(p => p.Bedenkzeit).HasConversion<string>();
        builder.Property(p => p.Status).HasConversion<string>();
        builder.Property(p => p.Ergebnis).HasConversion<string>();
        builder.Property(p => p.Grund).HasConversion<string>();
        builder.Property(p => p.Fen).IsRequired();
        builder.Property(p => p.Version).IsConcurrencyToken();

        // Konto loeschen kaskadiert auf alle Partien (SCHACH-SPEC 8.12, 9.7).
        builder.HasOne<Spieler>()
            .WithMany()
            .HasForeignKey(p => p.SpielerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Zuege)
            .WithOne()
            .HasForeignKey(z => z.PartieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Sprueche)
            .WithOne()
            .HasForeignKey(s => s.PartieId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.SpielerId)
            .IsUnique()
            .HasFilter(OffenFilter)
            .HasDatabaseName("IX_Partie_SpielerId_Offen");

        // Sweep A, B, C (SCHACH-SPEC 8.7).
        builder.HasIndex(p => new { p.Status, p.LetzterZugAm });
    }
}
