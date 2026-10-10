using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Auth.PasswordReset;

namespace RecipeApp.Api.UnitTests.Auth;

public class PasswordResetServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordResetTokenRepository _tokens = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IEmailSender _email = Substitute.For<IEmailSender>();
    private readonly ManualTimeProvider _time = new(Start);
    private readonly User _user = new() { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "old-hash" };
    private readonly PasswordResetService _sut;

    public PasswordResetServiceTests()
    {
        _users.GetByEmailAsync("user@example.com", Arg.Any<CancellationToken>()).Returns(_user);
        _users.GetByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _hasher.Hash("new-password").Returns("new-hash");
        _sut = new PasswordResetService(
            _users, _tokens, _hasher, _email, _time,
            Options.Create(new PasswordResetOptions { PublicBaseUrl = "https://app.example.com" }),
            NullLogger<PasswordResetService>.Instance);
    }

    [Fact]
    public async Task RequestAsync_RegisteredEmail_SendsALinkWithAnUnstoredRawToken()
    {
        PasswordResetToken? stored = null;
        await _tokens.AddAsync(Arg.Do<PasswordResetToken>(token => stored = token), Arg.Any<CancellationToken>());
        EmailMessage? sent = null;
        await _email.SendAsync(Arg.Do<EmailMessage>(message => sent = message), Arg.Any<CancellationToken>());

        await _sut.RequestAsync("  User@Example.com ", CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.To.Should().Be("user@example.com");
        var rawToken = ExtractToken(sent.Body);
        stored.Should().NotBeNull();
        stored!.TokenHash.Should().NotBe(rawToken).And.Be(PasswordResetTokenHasher.Hash(rawToken));
        stored.UserId.Should().Be(_user.Id);
        stored.Consumed.Should().BeFalse();
        stored.ExpiresAtUtc.Should().Be(Start.UtcDateTime.AddHours(1));
        sent.Body.Should().Contain("https://app.example.com/reset-password?token=");
    }

    [Fact]
    public async Task RequestAsync_RegisteredEmail_InvalidatesEarlierOutstandingTokens()
    {
        await _sut.RequestAsync("user@example.com", CancellationToken.None);

        await _tokens.Received(1).InvalidateOutstandingAsync(_user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_UnknownEmail_SendsAndStoresNothingAndDoesNotThrow()
    {
        await _sut.RequestAsync("nobody@example.com", CancellationToken.None);

        await _email.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _tokens.DidNotReceive().AddAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestAsync_WhenEmailDeliveryFails_DoesNotThrow()
    {
        _email.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var act = () => _sut.RequestAsync("user@example.com", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetAsync_ValidToken_UpdatesPasswordHashConsumesTokenAndClearsLockout()
    {
        var token = IssueToken(out var rawToken);
        _tokens.TryConsumeAsync(token.Id, Arg.Any<CancellationToken>()).Returns(true);
        _user.FailedLoginAttempts = 5;
        _user.LockedUntilUtc = Start.UtcDateTime.AddMinutes(10);

        await _sut.ResetAsync(rawToken, "new-password", CancellationToken.None);

        _user.PasswordHash.Should().Be("new-hash");
        _user.FailedLoginAttempts.Should().Be(0);
        _user.LockedUntilUtc.Should().BeNull();
        await _users.Received(1).UpdateAsync(_user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetAsync_UnknownToken_ThrowsInvalidResetToken()
    {
        var act = () => _sut.ResetAsync("not-a-token", "new-password", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidResetTokenException>();
        await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetAsync_TokenOlderThanOneHour_ThrowsInvalidResetToken()
    {
        var token = IssueToken(out var rawToken);
        _tokens.TryConsumeAsync(token.Id, Arg.Any<CancellationToken>()).Returns(true);
        _time.Advance(TimeSpan.FromHours(1));

        var act = () => _sut.ResetAsync(rawToken, "new-password", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidResetTokenException>();
        _user.PasswordHash.Should().Be("old-hash");
    }

    [Fact]
    public async Task ResetAsync_TokenJustInsideOneHour_Succeeds()
    {
        var token = IssueToken(out var rawToken);
        _tokens.TryConsumeAsync(token.Id, Arg.Any<CancellationToken>()).Returns(true);
        _time.Advance(TimeSpan.FromMinutes(59));

        await _sut.ResetAsync(rawToken, "new-password", CancellationToken.None);

        _user.PasswordHash.Should().Be("new-hash");
    }

    [Fact]
    public async Task ResetAsync_AlreadyConsumedToken_ThrowsInvalidResetToken()
    {
        var token = IssueToken(out var rawToken);
        token.Consumed = true;

        var act = () => _sut.ResetAsync(rawToken, "new-password", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidResetTokenException>();
    }

    [Fact]
    public async Task ResetAsync_WhenAConcurrentRequestConsumesTheTokenFirst_ThrowsInvalidResetToken()
    {
        var token = IssueToken(out var rawToken);
        _tokens.TryConsumeAsync(token.Id, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => _sut.ResetAsync(rawToken, "new-password", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidResetTokenException>();
        _user.PasswordHash.Should().Be("old-hash");
    }

    private PasswordResetToken IssueToken(out string rawToken)
    {
        rawToken = "raw-token-value";
        var token = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id,
            TokenHash = PasswordResetTokenHasher.Hash(rawToken),
            CreatedAtUtc = Start.UtcDateTime,
            ExpiresAtUtc = Start.UtcDateTime.AddHours(1)
        };
        _tokens.FindByHashAsync(token.TokenHash, Arg.Any<CancellationToken>()).Returns(token);
        return token;
    }

    private static string ExtractToken(string body)
    {
        var marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOfAny([' ', '\r', '\n'], start);
        return Uri.UnescapeDataString(end < 0 ? body[start..] : body[start..end]);
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
