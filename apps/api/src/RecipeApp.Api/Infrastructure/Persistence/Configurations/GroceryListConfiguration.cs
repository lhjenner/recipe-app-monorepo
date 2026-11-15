using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class GroceryListConfiguration : IEntityTypeConfiguration<GroceryList>
{
    public void Configure(EntityTypeBuilder<GroceryList> list)
    {
        list.ToTable("GroceryLists");
        list.HasKey(entity => entity.Id);
        list.Property(entity => entity.Id).ValueGeneratedNever();
        list.Property(entity => entity.CreatedAtUtc).IsRequired();
        list.Property(entity => entity.UpdatedAtUtc).IsRequired();
        list.Property(entity => entity.Version).IsRowVersion();
        list.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId })
            .HasName("AK_GroceryLists_Id_OwnerUserId");
        list.HasIndex(entity => entity.OwnerUserId)
            .IsUnique()
            .HasDatabaseName("IX_GroceryLists_OwnerUserId");
        list.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
