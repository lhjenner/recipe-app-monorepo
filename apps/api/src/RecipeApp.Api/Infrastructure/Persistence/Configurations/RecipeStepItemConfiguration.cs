using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class RecipeStepItemConfiguration : IEntityTypeConfiguration<RecipeStepItem>
{
    public void Configure(EntityTypeBuilder<RecipeStepItem> item)
    {
        item.ToTable("RecipeStepItems", table =>
        {
            table.HasCheckConstraint("CK_RecipeStepItems_Position_Nonnegative", "\"Position\" >= 0");
            table.HasCheckConstraint(
                "CK_RecipeStepItems_Kind_IngredientId",
                "\"Kind\" IN (0, 1, 2) AND ((\"Kind\" = 0 AND \"IngredientId\" IS NOT NULL) OR (\"Kind\" IN (1, 2) AND \"IngredientId\" IS NULL))");
        });
        item.HasKey(entity => entity.Id);
        item.Property(entity => entity.Id).ValueGeneratedNever();
        item.Property(entity => entity.Kind).HasConversion<int>();
        item.Property(entity => entity.Text).IsRequired();
        item.HasIndex(entity => new { entity.RecipeStepId, entity.Position })
            .IsUnique()
            .HasDatabaseName("IX_RecipeStepItems_RecipeStepId_Position");
        item.HasOne<RecipeStep>()
            .WithMany(entity => entity.Items)
            .HasForeignKey(entity => new { entity.RecipeStepId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Cascade);
        item.HasOne(entity => entity.Ingredient)
            .WithMany()
            .HasForeignKey(entity => new { entity.IngredientId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
