using FluentAssertions;
using NSubstitute;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.UnitTests.Auth;

public class AccountLockoutTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ManualTimeProvider _time = new(Start);
    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hash" };
    private readonly AuthService _sut;

    public AccountLockoutTests()
    {
        _users.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(_user);
        _hasher.Verify("correct", "hash").Returns(true);
        _hasher.Verify("wrong", "hash").Returns(false);
        _sut = new AuthService(_users, _hasher, _time);
    }

    [Fact]
    public async Task LoginAsync_FourConsecutiveFailures_DoesNotLockTheAccount()
    {
        await FailLoginAsync(4);

        _user.FailedLoginAttempts.Should().Be(4);
        _user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_FifthConsecutiveFailure_LocksTheAccountForFifteenMinutes()
    {
        await FailLoginAsync(5);

        _user.LockedUntilUtc.Should().Be(Start.UtcDateTime.AddMinutes(15));
        await _users.Received(5).UpdateAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WhileLocked_ThrowsAccountLockedEvenWithCorrectPassword()
    {
        await FailLoginAsync(5);
        _time.Advance(TimeSpan.FromMinutes(14));

        var act = () => _sut.LoginAsync(new LoginRequest("user@example.com", "correct"), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<AccountLockedException>()).Which;
        exception.LockedUntilUtc.Should().Be(Start.UtcDateTime.AddMinutes(15));
    }

    [Fact]
    public async Task LoginAsync_FailureWhileLocked_DoesNotExtendTheLock()
    {
        await FailLoginAsync(5);
        _time.Advance(TimeSpan.FromMinutes(5));

        var act = () => _sut.LoginAsync(new LoginRequest("user@example.com", "wrong"), CancellationToken.None);
        await act.Should().ThrowAsync<AccountLockedException>();

        _user.LockedUntilUtc.Should().Be(Start.UtcDateTime.AddMinutes(15));
    }

    [Fact]
    public async Task LoginAsync_AfterLockoutExpires_AllowsLoginAndResetsTheCounter()
    {
        await FailLoginAsync(5);
        _time.Advance(TimeSpan.FromMinutes(15));

        var result = await _sut.LoginAsync(new LoginRequest("user@example.com", "correct"), CancellationToken.None);

        result.Email.Should().Be("user@example.com");
        _user.FailedLoginAttempts.Should().Be(0);
        _user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_FailureAfterLockoutExpires_StartsAFreshCount()
    {
        await FailLoginAsync(5);
        _time.Advance(TimeSpan.FromMinutes(15));

        await FailLoginAsync(1);

        _user.FailedLoginAttempts.Should().Be(1);
        _user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_Success_ResetsConsecutiveFailureCounter()
    {
        await FailLoginAsync(3);

        await _sut.LoginAsync(new LoginRequest("user@example.com", "correct"), CancellationToken.None);
        await FailLoginAsync(4);

        _user.FailedLoginAttempts.Should().Be(4);
        _user.LockedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_DoesNotPersistAnything()
    {
        var act = () => _sut.LoginAsync(new LoginRequest("nobody@example.com", "wrong"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    private async Task FailLoginAsync(int times)
    {
        for (var attempt = 0; attempt < times; attempt++)
        {
            var act = () => _sut.LoginAsync(new LoginRequest("user@example.com", "wrong"), CancellationToken.None);
            await act.Should().ThrowAsync<InvalidCredentialsException>();
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
