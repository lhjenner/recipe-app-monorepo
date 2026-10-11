using Microsoft.AspNetCore.Authentication;

namespace RecipeApp.Api.Infrastructure.Persistence;

// Single source of truth for session lifetime: used by the cookie options and the server-side ticket store.
public static class SessionPolicy
{
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromDays(14);

    public static DateTimeOffset GetExpiration(AuthenticationTicket ticket, DateTimeOffset now) =>
        ticket.Properties.ExpiresUtc
        ?? ticket.Properties.IssuedUtc?.Add(IdleTimeout)
        ?? now.Add(IdleTimeout);

    public static bool IsExpired(DateTimeOffset expiresAtUtc, DateTimeOffset now) => expiresAtUtc <= now;
}
