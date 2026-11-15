using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class GroceryRecipeAdditionConfiguration : IEntityTypeConfiguration<GroceryRecipeAddition>
{
    public void Configure(EntityTypeBuilder<GroceryRecipeAddition> addition)
    {
        addition.ToTable("GroceryRecipeAdditions");
        addition.HasKey(entity => entity.Id);
        addition.Property(entity => entity.Id).ValueGeneratedNever();
        addition.Property(entity => entity.RecipeNameSnapshot).IsRequired().HasColumnType("text");
        addition.Property(entity => entity.AddedAtUtc).IsRequired();
        addition.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId, entity.GroceryListId })
            .HasName("AK_GroceryRecipeAdditions_Id_Owner_List");
        addition.HasIndex(entity => new { entity.OwnerUserId, entity.GroceryListId, entity.RecipeId })
            .HasDatabaseName("IX_GroceryRecipeAdditions_Owner_List_Recipe");
        addition.HasOne(entity => entity.GroceryList)
            .WithMany(entity => entity.RecipeAdditions)
            .HasForeignKey(entity => new { entity.GroceryListId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
        addition.HasOne<Recipe>()
            .WithMany()
            .HasForeignKey(entity => new { entity.RecipeId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
