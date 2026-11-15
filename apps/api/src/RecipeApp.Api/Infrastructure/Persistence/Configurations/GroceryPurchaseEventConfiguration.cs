using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class GroceryPurchaseEventConfiguration : IEntityTypeConfiguration<GroceryPurchaseEvent>
{
    public void Configure(EntityTypeBuilder<GroceryPurchaseEvent> purchaseEvent)
    {
        purchaseEvent.ToTable("GroceryPurchaseEvents", table =>
        {
            table.HasCheckConstraint(
                "CK_GroceryPurchaseEvents_Kind_Reference",
                "\"Kind\" IN (0, 1) AND ((\"Kind\" = 0 AND \"ReversesEventId\" IS NULL) OR (\"Kind\" = 1 AND \"ReversesEventId\" IS NOT NULL))");
            table.HasCheckConstraint(
                "CK_GroceryPurchaseEvents_Quantity",
                "\"Quantity\" > 0");
            table.HasCheckConstraint(
                "CK_GroceryPurchaseEvents_Origin",
                "\"Origin\" IN (0, 1)");
            table.HasCheckConstraint(
                "CK_GroceryPurchaseEvents_Snapshot",
                "jsonb_typeof(\"AffectedContributionIdsJson\") = 'array'");
        });
        purchaseEvent.HasKey(entity => entity.Id);
        purchaseEvent.Property(entity => entity.Id).ValueGeneratedNever();
        purchaseEvent.Property(entity => entity.Kind).HasConversion<int>();
        purchaseEvent.Property(entity => entity.Origin).HasConversion<int>();
        purchaseEvent.Property(entity => entity.Quantity)
            .HasPrecision(GroceryDecimalContract.Precision, GroceryDecimalContract.Scale);
        purchaseEvent.Property(entity => entity.PurchaseUnit).IsRequired().HasMaxLength(40);
        purchaseEvent.Property(entity => entity.ItemNameSnapshot).IsRequired().HasMaxLength(200);
        purchaseEvent.Property(entity => entity.AffectedContributionIdsJson)
            .IsRequired()
            .HasColumnType("jsonb");
        purchaseEvent.Property(entity => entity.OccurredAtUtc).IsRequired();
        purchaseEvent.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId, entity.GroceryListId })
            .HasName("AK_GroceryPurchaseEvents_Id_Owner_List");
        purchaseEvent.HasIndex(entity => new
            {
                entity.OwnerUserId,
                entity.GroceryListId,
                entity.OccurredAtUtc,
                entity.Id
            })
            .HasDatabaseName("IX_GroceryPurchaseEvents_Owner_List_Date_Id");
        purchaseEvent.HasIndex(entity => new { entity.OwnerUserId, entity.ReversesEventId })
            .IsUnique()
            .HasFilter("\"ReversesEventId\" IS NOT NULL")
            .HasDatabaseName("IX_GroceryPurchaseEvents_OneReversalPerEvent");
        purchaseEvent.HasOne(entity => entity.GroceryList)
            .WithMany(entity => entity.PurchaseEvents)
            .HasForeignKey(entity => new { entity.GroceryListId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
        purchaseEvent.HasOne(entity => entity.GroceryItem)
            .WithMany(entity => entity.PurchaseEvents)
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
        purchaseEvent.HasOne(entity => entity.ReversesEvent)
            .WithMany()
            .HasForeignKey(entity => new
            {
                entity.ReversesEventId,
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
    }
}
