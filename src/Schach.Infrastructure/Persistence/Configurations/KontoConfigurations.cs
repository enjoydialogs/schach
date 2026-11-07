using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Modell;

namespace Schach.Infrastructure.Persistence.Configurations;

/// <summary><c>Spieler</c> nach SCHACH-SPEC 8.12 und 9.1.</summary>
internal sealed class SpielerConfiguration : IEntityTypeConfiguration<Spieler>
{
    public void Configure(EntityTypeBuilder<Spieler> builder)
    {
        builder.ToTable("Spieler");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Email).IsRequired();

        // Faengt das Rennen zweier Bestaetigungen fuer dieselbe Adresse ab (SCHACH-SPEC 9.3).
        builder.HasIndex(s => s.Email).IsUnique();
    }
}

/// <summary><c>Bilanz</c> nach SCHACH-SPEC 3.8 und 8.12.</summary>
internal sealed class BilanzConfiguration : IEntityTypeConfiguration<Bilanz>
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

/// <summary><c>Anmeldung</c> nach SCHACH-SPEC 8.12 und 9.2. Kein Fremdschluessel: Der Spieler
/// entsteht erst bei der Bestaetigung; Konto loeschen raeumt per <c>Email</c> ab (SCHACH-SPEC 9.7).</summary>
internal sealed class AnmeldungConfiguration : IEntityTypeConfiguration<Anmeldung>
{
    public void Configure(EntityTypeBuilder<Anmeldung> builder)
    {
        builder.ToTable("Anmeldung", t =>
        {
            t.HasCheckConstraint("CK_Anmeldung_TokenHash_32", "octet_length(\"TokenHash\") = 32");
            t.HasCheckConstraint("CK_Anmeldung_CodeHash_32", "octet_length(\"CodeHash\") = 32");
        });
        builder.HasKey(a => a.Id);
        // Zufall v4 aus der Anwendung (SCHACH-SPEC 9.2), keine sequenzielle Guid aus EF.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Email).IsRequired();
        builder.Property(a => a.TokenHash).IsRequired();
        builder.Property(a => a.CodeHash).IsRequired();

        builder.HasIndex(a => a.Email);
        builder.HasIndex(a => a.ErstelltAm);
        builder.HasIndex(a => a.TokenHash).IsUnique();
    }
}

/// <summary><c>AnmeldungVersuch</c> nach SCHACH-SPEC 8.12 und 11.3.</summary>
internal sealed class AnmeldungVersuchConfiguration : IEntityTypeConfiguration<AnmeldungVersuch>
{
    public void Configure(EntityTypeBuilder<AnmeldungVersuch> builder)
    {
        builder.ToTable("AnmeldungVersuch", t =>
            t.HasCheckConstraint("CK_AnmeldungVersuch_EmailHash_32", "octet_length(\"EmailHash\") = 32"));
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.EmailHash).IsRequired();

        // Limit je Adresse zaehlt Zeilen je Hash in einem Zeitfenster; Sweep D loescht nach Alter.
        builder.HasIndex(v => new { v.EmailHash, v.ErstelltAm });
        builder.HasIndex(v => v.ErstelltAm);
    }
}
