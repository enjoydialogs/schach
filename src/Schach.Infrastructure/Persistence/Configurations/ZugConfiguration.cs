using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Modell;

namespace Schach.Infrastructure.Persistence.Configurations;

/// <summary><c>Zug</c> nach SCHACH-SPEC 8.12. Der Primaerschluessel (PartieId, Halbzug) ist zugleich
/// der Unique-Index aus SCHACH-SPEC 8.8: Ein zweiter Zug am selben Halbzug scheitert.</summary>
internal sealed class ZugConfiguration : IEntityTypeConfiguration<Zug>
{
    public void Configure(EntityTypeBuilder<Zug> builder)
    {
        builder.ToTable("Zug");
        builder.HasKey(z => new { z.PartieId, z.Halbzug });
        builder.Property(z => z.Halbzug).ValueGeneratedNever();

        builder.Property(z => z.Seite).HasConversion<string>();
        builder.Property(z => z.Engine).HasConversion<string>();
        builder.Property(z => z.Uci).IsRequired();
        builder.Property(z => z.San).IsRequired();
        builder.Property(z => z.FenDanach).IsRequired();
    }
}
