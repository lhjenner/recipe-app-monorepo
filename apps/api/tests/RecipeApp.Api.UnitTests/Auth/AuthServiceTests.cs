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
        // "When asked to hash any password, return this pretend hash."
        _hasher.Hash("password123").Returns("hashed-password");

        var sut = new AuthService(_users, _hasher); // sut = "system under test"

        // ACT: call the method that doesn't exist yet.
        var result = await sut.RegisterAsync(
            new RegisterRequest("new@example.com", "password123"),
            CancellationToken.None);

        // ASSERT: the DTO shape from our discussion — Id, Email, CreatedAtUtc.
        result.Email.Should().Be("new@example.com");
        result.Id.Should().NotBeEmpty();

        // And the AC-17 guarantee: the service must have told the repository
        // to save the HASH, never the plaintext.
        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.PasswordHash == "hashed-password"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsException()
    {
        // ARRANGE: script the fakes.
        _users.ExistsByEmailAsync("existing@example.com", Arg.Any<CancellationToken>())
              .Returns(true);

        var sut = new AuthService(_users, _hasher);

        // ACT & ASSERT: expect an exception when trying to register with an existing email.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await sut.RegisterAsync(
                new RegisterRequest("existing@example.com", "password123"),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task RegisterAsync_WithInvalidEmail_ThrowsException()
    {
        // ARRANGE: script the fakes.
        var sut = new AuthService(_users, _hasher);

        // ACT & ASSERT: expect an exception when trying to register with an invalid email.
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await sut.RegisterAsync(
                new RegisterRequest("invalid-email", "password123"),
                CancellationToken.None);
        });
    }
    
    [Fact]
    public async Task RegisterAsync_WithoutHashedPassword_ThrowsException()
    {
        // ARRANGE: script the fakes.
        var sut = new AuthService(_users, _hasher);

        // ACT & ASSERT: expect an exception when trying to register without a hashed password.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await sut.RegisterAsync(
                new RegisterRequest("new@example.com", ""),
                CancellationToken.None);
        });
    }
}
