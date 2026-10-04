using FluentAssertions;
using NSubstitute;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.UnitTests.Auth;

public class AuthServiceTests
{
    // NSubstitute creates fake implementations of the two interfaces.
    // No database, no hashing library — just stand-ins we can script and inspect.
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    [Fact]
    public async Task RegisterAsync_WithNewEmail_ReturnsUserDto()
    {
        // ARRANGE: script the fakes.
        // "When asked if this email exists, say no."
        _users.ExistsByEmailAsync("new@example.com", Arg.Any<CancellationToken>())
              .Returns(false);
        // "When asked to hash this password, return this pretend hash."
        _hasher.Hash("password123").Returns("hashed-password");

        var sut = new AuthService(_users, _hasher); // sut = "system under test"

        // ACT
        var result = await sut.RegisterAsync(
            new RegisterRequest("new@example.com", "password123"),
            CancellationToken.None);

        // ASSERT
        result.Email.Should().Be("new@example.com");
        result.Id.Should().NotBeEmpty();

        // Verify the service passes the hasher output to persistence.
        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.PasswordHash == "hashed-password"),
            Arg.Any<CancellationToken>());
    }
}
