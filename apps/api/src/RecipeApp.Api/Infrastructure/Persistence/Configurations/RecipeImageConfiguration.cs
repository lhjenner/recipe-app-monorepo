using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class RecipeImageConfiguration : IEntityTypeConfiguration<RecipeImage>
{
    public void Configure(EntityTypeBuilder<RecipeImage> image)
    {
        image.ToTable("RecipeImages", table =>
        {
            table.HasCheckConstraint(
                "CK_RecipeImages_StorageMetadata",
                "\"ByteSize\" > 0 AND \"Width\" > 0 AND \"Height\" > 0 AND \"ObjectKey\" ~ '[^[:space:]]' AND \"ContentType\" ~ '[^[:space:]]'");
            table.HasCheckConstraint(
                "CK_RecipeImages_Sha256",
                "\"Sha256\" ~ '^[0-9A-Fa-f]{64}$'");
        });
        image.HasKey(entity => entity.RecipeId);
        image.Property(entity => entity.ObjectKey).IsRequired().HasMaxLength(1024);
        image.Property(entity => entity.ContentType).IsRequired().HasMaxLength(255);
        image.Property(entity => entity.Sha256).IsRequired().HasMaxLength(64);
        image.HasOne(entity => entity.Recipe)
            .WithOne(entity => entity.Image)
            .HasForeignKey<RecipeImage>(entity => entity.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
