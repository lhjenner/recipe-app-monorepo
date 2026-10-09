using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace RecipeApp.Api.ComponentTests.Auth;

public sealed class ApiAuthContext : IAsyncDisposable
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("recipeapp_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string OriginalSessionCookie { get; set; } = string.Empty;

    public HttpResponseMessage? Response { get; set; }

    public async Task StartAsync()
    {
        await _database.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:DefaultConnection", _database.GetConnectionString()));

        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await database.Database.MigrateAsync();
    }

    public async Task SeedExistingAccountAsync(string email, string password)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var normalizedEmail = email.Trim().ToLowerInvariant();

        await users.AddAsync(new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = hasher.Hash(password),
            CreatedAtUtc = DateTime.UtcNow
        }, CancellationToken.None);
    }

    public async Task<User?> FindUserAsync(string email)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await database.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Email == normalizedEmail);
    }

    public async ValueTask DisposeAsync()
    {
        Response?.Dispose();
        Client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _database.DisposeAsync();
    }
}
