using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Entitaeten;

namespace Schach.Infrastructure.Persistence.Konfiguration;

// Spieler und Bilanz nach SCHACH-SPEC 8.12. Konto loeschen kaskadiert in der Datenbank von Spieler
// auf Partie (und weiter auf Zug und Spruch) und auf Bilanz.

internal sealed class SpielerKonfiguration : IEntityTypeConfiguration<Spieler>
{
    public void Configure(EntityTypeBuilder<Spieler> builder)
    {
        builder.ToTable("Spieler");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Email).IsRequired();
        builder.HasIndex(s => s.Email).IsUnique();
    }
}

internal sealed class BilanzKonfiguration : IEntityTypeConfiguration<Bilanz>
{
    public void Configure(EntityTypeBuilder<Bilanz> builder)
    {
        builder.ToTable("Bilanz");
        builder.HasKey(b => new { b.SpielerId, b.GegnerId });

        builder.HasOne<Spieler>()
            .WithMany()
            .HasForeignKey(b => b.SpielerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
