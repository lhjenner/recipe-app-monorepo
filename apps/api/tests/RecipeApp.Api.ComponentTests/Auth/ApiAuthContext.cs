using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Auth.PasswordReset;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Images;
using RecipeApp.Api.ComponentTests.Images;
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

    public TestTimeProvider Time { get; } = new();

    public IServiceProvider Services => _factory!.Services;

    public CapturingEmailSender Emails { get; } = new();
    public SyntheticObjectStorage Storage { get; } = new();

    public HttpClient Client { get; private set; } = null!;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string OriginalSessionCookie { get; set; } = string.Empty;

    public HttpResponseMessage? Response { get; set; }

    public Guid ScenarioIngredientId { get; set; }

    public async Task StartAsync()
    {
        await _database.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("DATABASE_URL", "");
                builder.UseSetting("ConnectionStrings:DefaultConnection", _database.GetConnectionString());
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<TimeProvider>(Time);
                    services.AddSingleton<IEmailSender>(Emails);
                    services.AddSingleton<IPrivateObjectStorage>(Storage);
                });
            });

        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
