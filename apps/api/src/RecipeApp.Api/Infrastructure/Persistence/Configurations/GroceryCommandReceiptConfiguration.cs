using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class GroceryCommandReceiptConfiguration : IEntityTypeConfiguration<GroceryCommandReceipt>
{
    public void Configure(EntityTypeBuilder<GroceryCommandReceipt> receipt)
    {
        receipt.ToTable("GroceryCommandReceipts", table =>
        {
            table.HasCheckConstraint(
                "CK_GroceryCommandReceipts_CommandId",
                "\"CommandId\" <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint(
                "CK_GroceryCommandReceipts_CommandType",
                "\"CommandType\" ~ '[^[:space:]]'");
            table.HasCheckConstraint(
                "CK_GroceryCommandReceipts_Fingerprint",
                "\"RequestFingerprint\" ~ '^[0-9A-Fa-f]{64}$'");
            table.HasCheckConstraint(
                "CK_GroceryCommandReceipts_ResultJson",
                "jsonb_typeof(\"ResultJson\") = 'object'");
        });
        receipt.HasKey(entity => entity.Id);
        receipt.Property(entity => entity.Id).ValueGeneratedNever();
        receipt.Property(entity => entity.CommandType).IsRequired().HasMaxLength(64);
        receipt.Property(entity => entity.RequestFingerprint).IsRequired().HasMaxLength(64);
        receipt.Property(entity => entity.ResultJson).IsRequired().HasColumnType("jsonb");
        receipt.Property(entity => entity.ResultListVersion).IsRequired();
        receipt.Property(entity => entity.CommittedAtUtc).IsRequired();
        receipt.HasIndex(entity => new
            {
                entity.OwnerUserId,
                entity.GroceryListId,
                entity.CommandId
            })
            .IsUnique()
            .HasDatabaseName("IX_GroceryCommandReceipts_Owner_List_Command");
        receipt.HasOne(entity => entity.GroceryList)
            .WithMany(entity => entity.CommandReceipts)
            .HasForeignKey(entity => new { entity.GroceryListId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
