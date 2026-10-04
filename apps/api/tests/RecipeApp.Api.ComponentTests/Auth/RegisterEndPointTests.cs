using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.ComponentTests.Auth;

public class RegisterEndpointTests
{
    [Fact]
    public async Task PostRegister_WithValidRequest_Returns201WithUserDto()
    {
        // ARRANGE: script the fakes, then hand them to the DI container
        // that the in-memory API will use.
        var users = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        users.ExistsByEmailAsync("new@example.com", Arg.Any<CancellationToken>())
             .Returns(false);
        hasher.Hash("password123").Returns("hashed-password");

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Whatever the real wiring would have registered,
                    // these fakes now replace it.
                    services.AddSingleton(users);
                    services.AddSingleton(hasher);
                });
            });

        var client = factory.CreateClient();

        // ACT: real HTTP POST, real JSON serialization, real routing.
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "new@example.com", password = "password123" });

        // ASSERT: AC-15's HTTP half.
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<UserDto>();
        dto!.Email.Should().Be("new@example.com");
    }
}