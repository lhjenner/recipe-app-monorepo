using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> resetToken)
    {
        resetToken.ToTable("PasswordResetTokens");
        resetToken.HasKey(entity => entity.Id);
        resetToken.Property(entity => entity.TokenHash).IsRequired().HasMaxLength(64);
        resetToken.Property(entity => entity.CreatedAtUtc).IsRequired();
        resetToken.Property(entity => entity.ExpiresAtUtc).IsRequired();
        resetToken.HasIndex(entity => entity.TokenHash).IsUnique();
        resetToken.HasIndex(entity => entity.UserId);
        resetToken.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
