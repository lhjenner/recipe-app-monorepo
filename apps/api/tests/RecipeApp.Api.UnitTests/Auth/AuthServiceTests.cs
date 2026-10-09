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

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsUserDto()
    {
        // ARRANGE
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@example.com",
            PasswordHash = "hashed-password"
        };
        _users.GetByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
              .Returns(user);
        _hasher.Verify("password123", "hashed-password").Returns(true);

        var sut = new AuthService(_users, _hasher);

        // ACT
        var result = await sut.LoginAsync(
            new LoginRequest("existing@example.com", "password123"),
            CancellationToken.None);

        // ASSERT
        result.Email.Should().Be("existing@example.com");
        result.Id.Should().NotBeEmpty();
        _hasher.Received(1).Verify("password123", "hashed-password");
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsGenericInvalidCredentialsException()
    {
        // ARRANGE
        _users.GetByEmailAsync("unknown@example.com", Arg.Any<CancellationToken>())
              .Returns((User?)null);

        var sut = new AuthService(_users, _hasher);

        // ACT
        var act = () => sut.LoginAsync(
            new LoginRequest("unknown@example.com", "password123"),
            CancellationToken.None);

        // ASSERT
        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(act);
        exception.Message.Should().Be("Invalid email or password");
        _hasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsGenericInvalidCredentialsException()
    {
        // ARRANGE
        _users.GetByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
              .Returns(new User
              {
                  Id = Guid.NewGuid(),
                  Email = "existing@example.com",
                  PasswordHash = "hashed-password"
              });
        _hasher.Verify("wrong-password", "hashed-password").Returns(false);

        var sut = new AuthService(_users, _hasher);

        // ACT
        var act = () => sut.LoginAsync(
            new LoginRequest("existing@example.com", "wrong-password"),
            CancellationToken.None);

        // ASSERT
        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(act);
        exception.Message.Should().Be("Invalid email or password");
        _hasher.Received(1).Verify("wrong-password", "hashed-password");
    }
}
