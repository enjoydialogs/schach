using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Schach.Domain.Modell;

namespace Schach.Infrastructure.Persistence.Configurations;

/// <summary><c>LlmVerbrauch</c> nach SCHACH-SPEC 8.12 und 11.1. Eine Zeile je Monat.</summary>
internal sealed class LlmVerbrauchConfiguration : IEntityTypeConfiguration<LlmVerbrauch>
{
    public void Configure(EntityTypeBuilder<LlmVerbrauch> builder)
    {
        builder.ToTable("LlmVerbrauch");
        builder.HasKey(l => l.Monat);
        builder.Property(l => l.Monat).ValueGeneratedNever();
    }
}
