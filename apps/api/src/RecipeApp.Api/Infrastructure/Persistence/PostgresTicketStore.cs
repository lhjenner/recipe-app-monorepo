using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class PostgresTicketStore(IDbContextFactory<AuthDbContext> databaseFactory) : ITicketStore
{
    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var id = Guid.NewGuid().ToString("N");
        await using var database = await databaseFactory.CreateDbContextAsync();
        database.AuthSessions.Add(new AuthSession
        {
            Id = id,
            TicketData = TicketSerializer.Default.Serialize(ticket),
            ExpiresAtUtc = GetExpiration(ticket)
        });
        await database.SaveChangesAsync();
        return id;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        await using var database = await databaseFactory.CreateDbContextAsync();
        var session = await database.AuthSessions.SingleOrDefaultAsync(item => item.Id == key);
        if (session is null)
        {
            return;
        }

        session.TicketData = TicketSerializer.Default.Serialize(ticket);
        session.ExpiresAtUtc = GetExpiration(ticket);
        await database.SaveChangesAsync();
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        await using var database = await databaseFactory.CreateDbContextAsync();
        var session = await database.AuthSessions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == key);

        if (session is null)
        {
            return null;
        }

        if (session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await database.AuthSessions.Where(item => item.Id == key).ExecuteDeleteAsync();
            return null;
        }

        return TicketSerializer.Default.Deserialize(session.TicketData);
    }

    public async Task RemoveAsync(string key)
    {
        await using var database = await databaseFactory.CreateDbContextAsync();
        await database.AuthSessions.Where(item => item.Id == key).ExecuteDeleteAsync();
    }

    private static DateTimeOffset GetExpiration(AuthenticationTicket ticket) =>
        ticket.Properties.ExpiresUtc
        ?? ticket.Properties.IssuedUtc?.Add(TimeSpan.FromDays(14))
        ?? DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(14));
}
