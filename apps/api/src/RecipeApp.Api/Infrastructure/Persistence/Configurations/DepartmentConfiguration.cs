using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> department)
    {
        department.ToTable("Departments");
        department.HasKey(entity => entity.Id);
        department.Property(entity => entity.Name).IsRequired().HasMaxLength(100);
        department.Property(entity => entity.NormalizedName).IsRequired().HasMaxLength(100);
        department.HasIndex(entity => new { entity.OwnerUserId, entity.NormalizedName })
            .IsUnique()
            .HasDatabaseName("IX_Departments_OwnerUserId_NormalizedName");
        department.HasOne<RecipeApp.Api.Auth.User>()
            .WithMany()
            .HasForeignKey(entity => entity.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
