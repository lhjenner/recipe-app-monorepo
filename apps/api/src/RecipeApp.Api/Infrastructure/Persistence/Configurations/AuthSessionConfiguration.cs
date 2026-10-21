using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.Infrastructure.Persistence.Configurations;

public sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> session)
    {
        session.ToTable("AuthSessions");
        session.HasKey(entity => entity.Id);
        session.Property(entity => entity.Id).HasMaxLength(32).IsRequired();
        session.Property(entity => entity.TicketData).IsRequired();
        session.Property(entity => entity.ExpiresAtUtc).IsRequired();
        session.HasIndex(entity => entity.ExpiresAtUtc);
    }
}
