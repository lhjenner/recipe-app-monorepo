using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> ingredient)
    {
        ingredient.ToTable("Ingredients");
        ingredient.HasKey(entity => entity.Id);
        ingredient.Property(entity => entity.Id).ValueGeneratedNever();
        ingredient.Property(entity => entity.Name).IsRequired().HasMaxLength(200);
        ingredient.Property(entity => entity.NormalizedName).IsRequired().HasMaxLength(200);
        ingredient.Property(entity => entity.NutritionSource);
        ingredient.Property(entity => entity.CreatedAtUtc).IsRequired();
        ingredient.Property(entity => entity.UpdatedAtUtc).IsRequired();
        ingredient.Property(entity => entity.Version).IsRowVersion();
        ingredient.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId })
            .HasName("AK_Ingredients_Id_OwnerUserId");
        ingredient.HasIndex(entity => new { entity.OwnerUserId, entity.NormalizedName })
            .IsUnique()
            .HasDatabaseName("IX_Ingredients_OwnerUserId_NormalizedName");
        ingredient.HasIndex(entity => new
            {
                entity.OwnerUserId,
                entity.ArchivedAtUtc,
                entity.NormalizedName
            })
            .HasDatabaseName("IX_Ingredients_OwnerUserId_ArchivedAtUtc_NormalizedName");
        ingredient.HasOne<RecipeApp.Api.Auth.User>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
        ingredient.HasOne(entity => entity.Category)
            .WithMany()
            .HasForeignKey(entity => entity.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        ingredient.OwnsOne(entity => entity.Nutrition, nutrition =>
        {
            nutrition.Property(value => value.EnergyKcal).HasColumnName("EnergyKcal").HasPrecision(9, 3);
            nutrition.Property(value => value.ProteinG).HasColumnName("ProteinG").HasPrecision(9, 3);
            nutrition.Property(value => value.CarbohydrateG).HasColumnName("CarbohydrateG").HasPrecision(9, 3);
            nutrition.Property(value => value.SugarG).HasColumnName("SugarG").HasPrecision(9, 3);
            nutrition.Property(value => value.FatG).HasColumnName("FatG").HasPrecision(9, 3);
            nutrition.Property(value => value.SaturatedFatG).HasColumnName("SaturatedFatG").HasPrecision(9, 3);
            nutrition.Property(value => value.FibreG).HasColumnName("FibreG").HasPrecision(9, 3);
            nutrition.Property(value => value.SodiumMg).HasColumnName("SodiumMg").HasPrecision(9, 3);
        });
        ingredient.Navigation(entity => entity.Nutrition).IsRequired(false);
    }
}
