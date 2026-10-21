using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> user)
    {
        user.ToTable("Users");
        user.HasKey(entity => entity.Id);
        user.Property(entity => entity.Email).IsRequired().HasMaxLength(320);
        user.HasIndex(entity => entity.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");
        user.Property(entity => entity.PasswordHash).IsRequired().HasMaxLength(100);
        user.Property(entity => entity.CreatedAtUtc).IsRequired();
        user.Property(entity => entity.FailedLoginAttempts).HasDefaultValue(0);
    }
}
