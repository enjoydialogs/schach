using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Entitaeten;

namespace Schach.Infrastructure.Persistence.Konfiguration;

// SpruchStimme und SprachausgabeVerbrauch nach SCHACH-SPEC 6.9, 8.12. Keine Fremdschluessel.

internal sealed class SpruchStimmeKonfiguration : IEntityTypeConfiguration<SpruchStimme>
{
    public void Configure(EntityTypeBuilder<SpruchStimme> builder)
    {
        builder.ToTable("SpruchStimme", t =>
            t.HasCheckConstraint("CK_SpruchStimme_Hash_32", "octet_length(\"Hash\") = 32"));
        builder.HasKey(s => s.Hash);
        builder.Property(s => s.Hash).ValueGeneratedNever();
        builder.Property(s => s.Audio).IsRequired();

        builder.HasIndex(s => s.ZuletztGenutztAm);
    }
}

internal sealed class SprachausgabeVerbrauchKonfiguration : IEntityTypeConfiguration<SprachausgabeVerbrauch>
{
    public void Configure(EntityTypeBuilder<SprachausgabeVerbrauch> builder)
    {
        builder.ToTable("SprachausgabeVerbrauch");
        builder.HasKey(v => v.Monat);
        builder.Property(v => v.Monat).ValueGeneratedNever();
    }
}
