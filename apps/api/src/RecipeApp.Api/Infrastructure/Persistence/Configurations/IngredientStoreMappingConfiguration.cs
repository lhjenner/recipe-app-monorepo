using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class IngredientStoreMappingConfiguration : IEntityTypeConfiguration<IngredientStoreMapping>
{
    public void Configure(EntityTypeBuilder<IngredientStoreMapping> mapping)
    {
        mapping.ToTable("IngredientStoreMappings", table =>
            table.HasCheckConstraint(
                "CK_IngredientStoreMappings_DepartmentOrNotStocked",
                "(\"DepartmentId\" IS NOT NULL AND NOT \"IsNotStocked\") OR (\"DepartmentId\" IS NULL AND \"IsNotStocked\")"));
        mapping.HasKey(entity => new { entity.IngredientId, entity.StoreId });
        mapping.HasOne<Ingredient>()
            .WithMany(entity => entity.StoreMappings)
            .HasForeignKey(entity => entity.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
        mapping.HasOne(entity => entity.Store)
            .WithMany()
            .HasForeignKey(entity => entity.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        mapping.HasOne(entity => entity.Department)
            .WithMany()
            .HasForeignKey(entity => entity.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
