using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.ComponentTests.Auth;

public class DuplicateEmailTests
{
    [Fact]
    public async Task Should_Return_Error_When_Email_Is_Duplicate()
    {
        // Arrange
        var user = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        user.ExistsByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
             .Returns(true);
        hasher.Hash("password123").Returns("hashed-password");

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(user);
                    services.AddSingleton(hasher);
                });
            });

        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "existing@example.com", password = "password123" });
        
        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }
}
