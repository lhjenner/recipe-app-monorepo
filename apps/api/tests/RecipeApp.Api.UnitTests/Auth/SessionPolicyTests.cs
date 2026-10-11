using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.UnitTests.Auth;

public sealed class SessionPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IdleTimeout_IsFourteenDays() =>
        SessionPolicy.IdleTimeout.Should().Be(TimeSpan.FromDays(14));

    [Fact]
    public void GetExpiration_UsesTheTicketExpiryWhenPresent()
    {
        var expires = Start.AddDays(3);
        var ticket = TicketExpiring(expires);

        SessionPolicy.GetExpiration(ticket, Start).Should().Be(expires);
    }

    [Fact]
    public void GetExpiration_FallsBackToIssuedPlusIdleTimeout()
    {
        var ticket = new AuthenticationTicket(new(), new AuthenticationProperties { IssuedUtc = Start }, "test");

        SessionPolicy.GetExpiration(ticket, Start.AddDays(5)).Should().Be(Start.AddDays(14));
    }

    [Fact]
    public void GetExpiration_FallsBackToNowPlusIdleTimeoutWhenNothingIsSet()
    {
        var ticket = new AuthenticationTicket(new(), new AuthenticationProperties(), "test");

        SessionPolicy.GetExpiration(ticket, Start).Should().Be(Start.AddDays(14));
    }

    [Fact]
    public void ActivityRenewsTheSession_SoItOutlivesTheOriginalExpiry()
    {
        var firstExpiry = SessionPolicy.GetExpiration(TicketExpiring(Start.AddDays(14)), Start);
        var dayTen = Start.AddDays(10);
        var renewedExpiry = SessionPolicy.GetExpiration(TicketExpiring(dayTen.Add(SessionPolicy.IdleTimeout)), dayTen);

        SessionPolicy.IsExpired(firstExpiry, Start.AddDays(15)).Should().BeTrue();
        SessionPolicy.IsExpired(renewedExpiry, Start.AddDays(15)).Should().BeFalse();
    }

    [Theory]
    [InlineData(13, false)]
    [InlineData(14, true)]
    [InlineData(15, true)]
    public void IsExpired_AfterIdleTimeoutWithoutActivity(int daysIdle, bool expected)
    {
        var expiry = SessionPolicy.GetExpiration(TicketExpiring(Start.Add(SessionPolicy.IdleTimeout)), Start);

        SessionPolicy.IsExpired(expiry, Start.AddDays(daysIdle)).Should().Be(expected);
    }

    private static AuthenticationTicket TicketExpiring(DateTimeOffset expires) =>
        new(new(), new AuthenticationProperties { ExpiresUtc = expires }, "test");
}
