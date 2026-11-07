using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Entitaeten;

namespace Schach.Infrastructure.Persistence.Konfiguration;

// Anmeldung, AnmeldungVersuch und LlmVerbrauch nach SCHACH-SPEC 8.12. Keine Fremdschluessel: die
// Zeilen haengen an einer Adresse oder einem Monat, nicht an einem Spieler.
// Postgres kennt kein bytea(32); die Laenge der SHA-256-Hashes sichert ein Check-Constraint.

internal sealed class AnmeldungKonfiguration : IEntityTypeConfiguration<Anmeldung>
{
    public void Configure(EntityTypeBuilder<Anmeldung> builder)
    {
        builder.ToTable("Anmeldung", t =>
        {
            t.HasCheckConstraint("CK_Anmeldung_TokenHash_32", "octet_length(\"TokenHash\") = 32");
            t.HasCheckConstraint("CK_Anmeldung_CodeHash_32", "octet_length(\"CodeHash\") = 32");
        });
        builder.HasKey(a => a.Id);
        // Zufall v4 vom Aufrufer (SCHACH-SPEC 8.12), nicht der zeitgeordnete Standard-Generator.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Email).IsRequired();
        builder.Property(a => a.TokenHash).IsRequired();
        builder.Property(a => a.CodeHash).IsRequired();

        builder.HasIndex(a => a.Email);
        builder.HasIndex(a => a.ErstelltAm);
        builder.HasIndex(a => a.TokenHash).IsUnique();
    }
}

internal sealed class AnmeldungVersuchKonfiguration : IEntityTypeConfiguration<AnmeldungVersuch>
{
    public void Configure(EntityTypeBuilder<AnmeldungVersuch> builder)
    {
        builder.ToTable("AnmeldungVersuch", t =>
            t.HasCheckConstraint("CK_AnmeldungVersuch_EmailHash_32", "octet_length(\"EmailHash\") = 32"));
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.EmailHash).IsRequired();

        builder.HasIndex(v => new { v.EmailHash, v.ErstelltAm });
        builder.HasIndex(v => v.ErstelltAm);
    }
}

internal sealed class LlmVerbrauchKonfiguration : IEntityTypeConfiguration<LlmVerbrauch>
{
    public void Configure(EntityTypeBuilder<LlmVerbrauch> builder)
    {
        builder.ToTable("LlmVerbrauch");
        builder.HasKey(l => l.Monat);
        builder.Property(l => l.Monat).ValueGeneratedNever();
    }
}
