using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class RecipeImportProvenanceConfiguration : IEntityTypeConfiguration<RecipeImportProvenance>
{
    public void Configure(EntityTypeBuilder<RecipeImportProvenance> provenance)
    {
        provenance.ToTable("RecipeImportProvenance", table =>
        {
            table.HasCheckConstraint(
                "CK_RecipeImportProvenance_Hashes",
                "\"DocumentSha256\" ~ '^[0-9A-Fa-f]{64}$' AND \"SourceFileSha256\" ~ '^[0-9A-Fa-f]{64}$' AND \"DecisionFingerprint\" ~ '^[0-9A-Fa-f]{64}$'");
        });
        provenance.HasKey(entity => entity.RecipeId);
        provenance.Property(entity => entity.SourceDocumentJson).IsRequired().HasColumnType("jsonb");
        provenance.Property(entity => entity.DocumentSha256).IsRequired().HasMaxLength(64);
        provenance.Property(entity => entity.SourceFileSha256).IsRequired().HasMaxLength(64);
        provenance.Property(entity => entity.MappingVersion).IsRequired();
        provenance.Property(entity => entity.DecisionFingerprint).IsRequired().HasMaxLength(64);
        provenance.HasOne(entity => entity.Recipe)
            .WithOne(entity => entity.ImportProvenance)
            .HasForeignKey<RecipeImportProvenance>(entity => entity.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
