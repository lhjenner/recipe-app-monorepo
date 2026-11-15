using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class GroceryContributionConfiguration : IEntityTypeConfiguration<GroceryContribution>
{
    public void Configure(EntityTypeBuilder<GroceryContribution> contribution)
    {
        contribution.ToTable("GroceryContributions", table =>
        {
            table.HasCheckConstraint(
                "CK_GroceryContributions_Origin_State",
                "\"Origin\" IN (0, 1) AND \"State\" IN (0, 1, 2)");
            table.HasCheckConstraint(
                "CK_GroceryContributions_Amounts",
                "(\"InterpretedAmount\" IS NULL OR \"InterpretedAmount\" > 0) AND (\"ManualPurchaseIncrement\" IS NULL OR \"ManualPurchaseIncrement\" >= 0)");
            table.HasCheckConstraint(
                "CK_GroceryContributions_Source",
                "(\"Origin\" = 0 AND \"RecipeAdditionId\" IS NULL AND \"SourceRecipeId\" IS NULL) OR (\"Origin\" = 1 AND \"RecipeAdditionId\" IS NOT NULL AND \"SourceRecipeId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_GroceryContributions_CountLabel",
                "(\"Dimension\" = 0 AND \"CountLabel\" IS NOT NULL AND \"CountLabel\" ~ '[^[:space:]]') OR (\"Dimension\" IS DISTINCT FROM 0 AND \"CountLabel\" IS NULL)");
        });
        contribution.HasKey(entity => entity.Id);
        contribution.Property(entity => entity.Id).ValueGeneratedNever();
        contribution.Property(entity => entity.Origin).HasConversion<int>();
        contribution.Property(entity => entity.State).HasConversion<int>();
        contribution.Property(entity => entity.Dimension).HasConversion<int?>();
        contribution.Property(entity => entity.CountLabel).HasMaxLength(40);
        contribution.Property(entity => entity.InterpretedAmount)
            .HasPrecision(GroceryDecimalContract.Precision, GroceryDecimalContract.Scale);
        contribution.Property(entity => entity.ManualPurchaseIncrement)
            .HasPrecision(GroceryDecimalContract.Precision, GroceryDecimalContract.Scale);
        contribution.Property(entity => entity.CreatedAtUtc).IsRequired();
        contribution.HasIndex(entity => new
            {
                entity.OwnerUserId,
                entity.GroceryListId,
                entity.GroceryItemId,
                entity.State
            })
            .HasDatabaseName("IX_GroceryContributions_Owner_List_Item_State");
        contribution.HasIndex(entity => new { entity.OwnerUserId, entity.RecipeAdditionId })
            .HasDatabaseName("IX_GroceryContributions_Owner_RecipeAddition");
        contribution.HasOne(entity => entity.GroceryList)
            .WithMany()
            .HasForeignKey(entity => new { entity.GroceryListId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
        contribution.HasOne(entity => entity.GroceryItem)
            .WithMany(entity => entity.Contributions)
            .HasForeignKey(entity => new
            {
                entity.GroceryItemId,
                entity.OwnerUserId,
                entity.GroceryListId
            })
            .HasPrincipalKey(entity => new
            {
                entity.Id,
                entity.OwnerUserId,
                entity.GroceryListId
            })
            .OnDelete(DeleteBehavior.Restrict);
        contribution.HasOne(entity => entity.RecipeAddition)
            .WithMany(entity => entity.Contributions)
            .HasForeignKey(entity => new
            {
                entity.RecipeAdditionId,
                entity.OwnerUserId,
                entity.GroceryListId
            })
            .HasPrincipalKey(entity => new
            {
                entity.Id,
                entity.OwnerUserId,
                entity.GroceryListId
            })
            .OnDelete(DeleteBehavior.Restrict);
        contribution.HasOne(entity => entity.SourceRecipe)
            .WithMany()
            .HasForeignKey(entity => new { entity.SourceRecipeId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
