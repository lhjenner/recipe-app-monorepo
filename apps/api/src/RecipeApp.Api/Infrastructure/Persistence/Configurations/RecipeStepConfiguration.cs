using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class RecipeStepConfiguration : IEntityTypeConfiguration<RecipeStep>
{
    public void Configure(EntityTypeBuilder<RecipeStep> step)
    {
        step.ToTable("RecipeSteps", table =>
            table.HasCheckConstraint("CK_RecipeSteps_Position_Nonnegative", "\"Position\" >= 0"));
        step.HasKey(entity => entity.Id);
        step.Property(entity => entity.Id).ValueGeneratedNever();
        step.Property(entity => entity.Instructions).IsRequired();
        step.HasAlternateKey(entity => new { entity.Id, entity.OwnerUserId })
            .HasName("AK_RecipeSteps_Id_OwnerUserId");
        step.HasIndex(entity => new { entity.RecipeId, entity.Position })
            .IsUnique()
            .HasDatabaseName("IX_RecipeSteps_RecipeId_Position");
        step.HasOne<Recipe>()
            .WithMany(entity => entity.Steps)
            .HasForeignKey(entity => new { entity.RecipeId, entity.OwnerUserId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OwnerUserId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
