using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class RecipeTagConfiguration : IEntityTypeConfiguration<RecipeTag>
{
    public void Configure(EntityTypeBuilder<RecipeTag> tag)
    {
        tag.ToTable("RecipeTags", table =>
            table.HasCheckConstraint("CK_RecipeTags_Position_Nonnegative", "\"Position\" >= 0"));
        tag.HasKey(entity => entity.Id);
        tag.Property(entity => entity.Id).ValueGeneratedNever();
        tag.Property(entity => entity.Text).IsRequired();
        tag.HasIndex(entity => new { entity.RecipeId, entity.Position })
            .IsUnique()
            .HasDatabaseName("IX_RecipeTags_RecipeId_Position");
        tag.HasOne<Recipe>()
            .WithMany(entity => entity.Tags)
            .HasForeignKey(entity => entity.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
