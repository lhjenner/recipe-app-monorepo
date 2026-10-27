using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> recipe)
    {
        recipe.ToTable("Recipes", table => table.HasCheckConstraint(
            "CK_Recipes_LegacyId_NotBlank",
            "\"LegacyId\" IS NULL OR \"LegacyId\" ~ '[^[:space:]]'"));
        recipe.HasKey(entity => entity.Id);
        recipe.Property(entity => entity.Id).ValueGeneratedNever();
        recipe.Property(entity => entity.LegacyId).HasColumnType("text");
        recipe.Property(entity => entity.Name).IsRequired();
        recipe.Property(entity => entity.NormalizedName).IsRequired();
        recipe.Property(entity => entity.SourceText);
        recipe.Property(entity => entity.SourceUrl);
        recipe.Property(entity => entity.ServingsText);
        recipe.Property(entity => entity.PrepTimeText);
        recipe.Property(entity => entity.CookTimeText);
        recipe.Property(entity => entity.Version).IsRowVersion();
        recipe.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId })
            .HasName("AK_Recipes_Id_OwnerUserId");
        recipe.HasIndex(entity => new { entity.OwnerUserId, entity.LegacyId })
            .IsUnique()
            .HasFilter("\"LegacyId\" IS NOT NULL")
            .HasDatabaseName("IX_Recipes_OwnerUserId_LegacyId");
        recipe.HasIndex(entity => new { entity.OwnerUserId, entity.NormalizedName })
            .HasDatabaseName("IX_Recipes_OwnerUserId_NormalizedName");
        recipe.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
