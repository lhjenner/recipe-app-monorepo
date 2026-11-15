using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class GroceryItemConfiguration : IEntityTypeConfiguration<GroceryItem>
{
    public void Configure(EntityTypeBuilder<GroceryItem> item)
    {
        item.ToTable("GroceryItems", table =>
        {
            table.HasCheckConstraint(
                "CK_GroceryItems_State_Dimension",
                "\"State\" IN (0, 1, 2) AND (\"Dimension\" IS NULL OR \"Dimension\" IN (0, 1, 2))");
            table.HasCheckConstraint(
                "CK_GroceryItems_OutstandingPurchaseQuantity_Nonnegative",
                "\"OutstandingPurchaseQuantity\" >= 0");
            table.HasCheckConstraint(
                "CK_GroceryItems_NameAndUnit_NotBlank",
                "\"NormalizedName\" ~ '[^[:space:]]' AND \"PurchaseUnit\" ~ '[^[:space:]]' AND \"ConsolidationKey\" ~ '[^[:space:]]'");
            table.HasCheckConstraint(
                "CK_GroceryItems_CountLabel",
                "(\"Dimension\" = 0 AND \"CountLabel\" IS NOT NULL AND \"CountLabel\" ~ '[^[:space:]]') OR (\"Dimension\" IS DISTINCT FROM 0 AND \"CountLabel\" IS NULL)");
        });
        item.HasKey(entity => entity.Id);
        item.Property(entity => entity.Id).ValueGeneratedNever();
        item.Property(entity => entity.NormalizedName).IsRequired().HasMaxLength(200);
        item.Property(entity => entity.NameSnapshot).IsRequired().HasMaxLength(200);
        item.Property(entity => entity.Note).HasMaxLength(250);
        item.Property(entity => entity.ConsolidationKey).IsRequired().HasMaxLength(512);
        item.Property(entity => entity.PurchaseUnit).IsRequired().HasMaxLength(40);
        item.Property(entity => entity.CountLabel).HasMaxLength(40);
        item.Property(entity => entity.Dimension).HasConversion<int?>();
        item.Property(entity => entity.State).HasConversion<int>();
        item.Property(entity => entity.OutstandingPurchaseQuantity)
            .HasPrecision(GroceryDecimalContract.Precision, GroceryDecimalContract.Scale);
        item.Property(entity => entity.CreatedAtUtc).IsRequired();
        item.Property(entity => entity.UpdatedAtUtc).IsRequired();
        item.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId })
            .HasName("AK_GroceryItems_Id_OwnerUserId");
        item.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId, entity.GroceryListId })
            .HasName("AK_GroceryItems_Id_OwnerUserId_GroceryListId");
        item.HasIndex(entity => new { entity.OwnerUserId, entity.GroceryListId, entity.State })
            .HasDatabaseName("IX_GroceryItems_OwnerUserId_GroceryListId_State");
        item.HasIndex(entity => new
            {
                entity.OwnerUserId,
                entity.GroceryListId,
                entity.ConsolidationKey
            })
            .IsUnique()
            .HasFilter("\"State\" = 0")
            .HasDatabaseName("IX_GroceryItems_ActiveConsolidationKey");
        item.HasOne(entity => entity.GroceryList)
            .WithMany(entity => entity.Items)
            .HasForeignKey(entity => new { entity.GroceryListId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
        item.HasOne(entity => entity.Ingredient)
            .WithMany()
            .HasForeignKey(entity => new { entity.IngredientId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
        item.HasOne<Store>()
            .WithMany()
            .HasForeignKey(entity => entity.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
        item.HasOne<Department>()
            .WithMany()
            .HasForeignKey(entity => new { entity.DepartmentId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
