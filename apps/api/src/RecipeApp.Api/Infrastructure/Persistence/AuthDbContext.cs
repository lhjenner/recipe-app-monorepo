using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("Users");
        user.HasKey(entity => entity.Id);
        user.Property(entity => entity.Email).IsRequired().HasMaxLength(320);
        user.HasIndex(entity => entity.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email");
        user.Property(entity => entity.PasswordHash).IsRequired().HasMaxLength(100);
        user.Property(entity => entity.CreatedAtUtc).IsRequired();
        user.Property(entity => entity.FailedLoginAttempts).HasDefaultValue(0);

        var session = modelBuilder.Entity<AuthSession>();
        session.ToTable("AuthSessions");
        session.HasKey(entity => entity.Id);
        session.Property(entity => entity.Id).HasMaxLength(32).IsRequired();
        session.Property(entity => entity.TicketData).IsRequired();
        session.Property(entity => entity.ExpiresAtUtc).IsRequired();
        session.HasIndex(entity => entity.ExpiresAtUtc);

        var resetToken = modelBuilder.Entity<PasswordResetToken>();
        resetToken.ToTable("PasswordResetTokens");
        resetToken.HasKey(entity => entity.Id);
        resetToken.Property(entity => entity.TokenHash).IsRequired().HasMaxLength(64);
        resetToken.Property(entity => entity.CreatedAtUtc).IsRequired();
        resetToken.Property(entity => entity.ExpiresAtUtc).IsRequired();
        resetToken.HasIndex(entity => entity.TokenHash).IsUnique();
        resetToken.HasIndex(entity => entity.UserId);
        resetToken.HasOne<User>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
