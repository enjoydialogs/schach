using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Modell;

namespace Schach.Infrastructure.Persistence.Configurations;

/// <summary><c>Spruch</c> nach SCHACH-SPEC 8.12. Bewusst kein Unique-Index auf
/// (PartieId, NachHalbzug): Bei Partieende durch einen Spielerzug gibt es zwei Sprueche (SCHACH-SPEC 6.2).</summary>
internal sealed class SpruchConfiguration : IEntityTypeConfiguration<Spruch>
{
    public void Configure(EntityTypeBuilder<Spruch> builder)
    {
        builder.ToTable("Spruch");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Ereignis).HasConversion<string>();
        builder.Property(s => s.Quelle).HasConversion<string>();
        builder.Property(s => s.Text).IsRequired();

        builder.HasIndex(s => new { s.PartieId, s.NachHalbzug });
    }
}
