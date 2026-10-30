using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace RecipeApp.Api.Infrastructure.Persistence;

public static class DatabaseConnection
{
    public static string Get(IConfiguration configuration)
    {
        var url = configuration["DATABASE_URL"];
        if (string.IsNullOrWhiteSpace(url))
        {
            return configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Configure DATABASE_URL or ConnectionStrings:DefaultConnection.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("postgres" or "postgresql")
            || string.IsNullOrEmpty(uri.Host) || uri.AbsolutePath.Length < 2)
        {
            throw InvalidUrl();
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (userInfo.Length != 2 || string.IsNullOrEmpty(userInfo[0])
            || !query.TryGetValue("sslmode", out var sslMode)
            || sslMode.Count != 1 || sslMode[0] is not ("require" or "verify-full")
            || query.Keys.Any(key => key is not ("sslmode" or "channel_binding")))
        {
            throw InvalidUrl();
        }

        var channelBinding = ChannelBinding.Prefer;
        if (query.TryGetValue("channel_binding", out var binding))
        {
            if (binding.Count != 1 || binding[0] != "require") throw InvalidUrl();
            channelBinding = ChannelBinding.Require;
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            Database = Uri.UnescapeDataString(uri.AbsolutePath[1..]),
            SslMode = SslMode.VerifyFull,
            ChannelBinding = channelBinding
        }.ConnectionString;
    }

    private static InvalidOperationException InvalidUrl() =>
        new("DATABASE_URL must be a PostgreSQL URL with verified TLS.");
}
