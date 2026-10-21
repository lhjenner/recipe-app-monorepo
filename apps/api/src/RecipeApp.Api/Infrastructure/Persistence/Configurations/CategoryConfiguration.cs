using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> category)
    {
        category.ToTable("Categories");
        category.HasKey(entity => entity.Id);
        category.Property(entity => entity.Name).IsRequired().HasMaxLength(100);
        category.Property(entity => entity.NormalizedName).IsRequired().HasMaxLength(100);
        category.HasIndex(entity => new { entity.OwnerUserId, entity.NormalizedName })
            .IsUnique()
            .HasDatabaseName("IX_Categories_OwnerUserId_NormalizedName");
        category.HasOne<RecipeApp.Api.Auth.User>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
