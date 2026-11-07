using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Entitaeten;

namespace Schach.Infrastructure.Persistence.Konfiguration;

// Partie, Zug und Spruch nach SCHACH-SPEC 8.12. Enums als Text (8.4a). Fremdschluessel ohne
// Navigationen: Session 0b legt nur die Huellen an, Navigationen aendern das Schema nicht.

internal sealed class PartieKonfiguration : IEntityTypeConfiguration<Partie>
{
    /// <summary>Filter des Index „hoechstens eine offene Partie je Spieler“. Status steht als Text.</summary>
    internal const string NurOffen = "\"Status\" = 'Offen'";

    public void Configure(EntityTypeBuilder<Partie> builder)
    {
        builder.ToTable("Partie");
        builder.HasKey(p => p.Id);
        // Der Aufrufer vergibt Guid.NewGuid() (v4, 128 Bit Zufall): die Id ist fuer Gaeste der Zugriffsschluessel.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.GegnerId).IsRequired();
        builder.Property(p => p.SpielerFarbe).HasConversion<string>();
        builder.Property(p => p.Bedenkzeit).HasConversion<string>();
        builder.Property(p => p.Status).HasConversion<string>();
        builder.Property(p => p.Ergebnis).HasConversion<string>();
        builder.Property(p => p.Grund).HasConversion<string>();
        builder.Property(p => p.Fen).IsRequired();
        builder.Property(p => p.Version).IsConcurrencyToken();

        builder.HasOne<Spieler>()
            .WithMany()
            .HasForeignKey(p => p.SpielerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.SpielerId)
            .IsUnique()
            .HasFilter(NurOffen)
            .HasDatabaseName("UX_Partie_SpielerId_Offen");
        builder.HasIndex(p => new { p.Status, p.LetzterZugAm });
    }
}

internal sealed class ZugKonfiguration : IEntityTypeConfiguration<Zug>
{
    public void Configure(EntityTypeBuilder<Zug> builder)
    {
        builder.ToTable("Zug");
        // Der Primaerschluessel ist der Unique-Index Zug(PartieId, Halbzug) aus SCHACH-SPEC 8.8.
        builder.HasKey(z => new { z.PartieId, z.Halbzug });
        builder.Property(z => z.Halbzug).ValueGeneratedNever();

        builder.Property(z => z.Seite).HasConversion<string>();
        builder.Property(z => z.Uci).IsRequired();
        builder.Property(z => z.San).IsRequired();
        builder.Property(z => z.FenDanach).IsRequired();
        builder.Property(z => z.Engine).HasConversion<string>();
        builder.OwnsOne(z => z.Wdl, wdl => wdl.ToJson("Wdl"));

        builder.HasOne<Partie>()
            .WithMany()
            .HasForeignKey(z => z.PartieId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SpruchKonfiguration : IEntityTypeConfiguration<Spruch>
{
    public void Configure(EntityTypeBuilder<Spruch> builder)
    {
        builder.ToTable("Spruch");
        // Ersatzschluessel: nach einem Halbzug sind mehrere Sprueche moeglich, darum bewusst kein
        // Unique-Index auf (PartieId, NachHalbzug).
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Ereignis).HasConversion<string>();
        builder.Property(s => s.Text).IsRequired();
        builder.Property(s => s.Quelle).HasConversion<string>();

        builder.HasOne<Partie>()
            .WithMany()
            .HasForeignKey(s => s.PartieId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
