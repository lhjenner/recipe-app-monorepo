using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class ProductLinkConfiguration : IEntityTypeConfiguration<ProductLink>
{
    public void Configure(EntityTypeBuilder<ProductLink> link)
    {
        link.ToTable("ProductLinks");
        link.HasKey(entity => entity.Id);
        link.Property(entity => entity.Id).ValueGeneratedNever();
        link.Property(entity => entity.Url).IsRequired().HasMaxLength(2048);
        link.Property(entity => entity.VariantLabel).HasMaxLength(200);
        link.Property(entity => entity.Note).HasMaxLength(1000);
        link.HasIndex(entity => new { entity.IngredientId, entity.ChainId, entity.Position })
            .IsUnique()
            .HasDatabaseName("IX_ProductLinks_IngredientId_ChainId_Position");
        link.HasIndex(entity => new { entity.IngredientId, entity.ChainId })
            .IsUnique()
            .HasFilter("\"IsPreferred\" = TRUE")
            .HasDatabaseName("IX_ProductLinks_IngredientId_ChainId_Preferred");
        link.HasOne<Ingredient>()
            .WithMany(entity => entity.ProductLinks)
            .HasForeignKey(entity => entity.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
        link.HasOne(entity => entity.Chain)
            .WithMany()
            .HasForeignKey(entity => entity.ChainId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
