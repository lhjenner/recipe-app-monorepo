using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

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
    }
}
