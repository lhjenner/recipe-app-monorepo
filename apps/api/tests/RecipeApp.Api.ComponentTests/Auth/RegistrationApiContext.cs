using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.ComponentTests.Auth;

public sealed class RegistrationApiContext : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public RegistrationApiContext()
    {
        UserRepository = Substitute.For<IUserRepository>();
        PasswordHasher = Substitute.For<IPasswordHasher>();
        PasswordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(UserRepository);
                    services.AddSingleton(PasswordHasher);
                });
            });

        Client = _factory.CreateClient();
    }

    public IUserRepository UserRepository { get; }

    public IPasswordHasher PasswordHasher { get; }

    public HttpClient Client { get; }

    public string Email { get; set; } = string.Empty;

    public HttpResponseMessage? Response { get; set; }

    public void Dispose()
    {
        Response?.Dispose();
        Client.Dispose();
        _factory.Dispose();
    }
}
