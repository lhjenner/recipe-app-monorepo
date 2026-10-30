using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AppDbContextDesignTimeFactory>()
            .AddEnvironmentVariables()
            .Build();
        var directUrl = configuration["DATABASE_URL_UNPOOLED"];
        if (!string.IsNullOrWhiteSpace(directUrl))
        {
            configuration["DATABASE_URL"] = directUrl;
        }

        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(DatabaseConnection.Get(configuration)).Options);
    }
}
