using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Auth.PasswordReset;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Auth.PasswordReset;

[Binding]
public sealed partial class PasswordResetSteps(ApiAuthContext context)
{
    [GeneratedRegex(@"token=(?<token>[^\s&]+)")]
    private static partial Regex TokenPattern();

    [Given("email delivery is failing")]
    public void GivenEmailDeliveryIsFailing() => context.Emails.ShouldFail = true;

    [Given("I have requested a password reset for {string}")]
    [When("I request a password reset for {string}")]
    public async Task WhenIRequestAPasswordResetFor(string email)
    {
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
    }

    [When("I reset the password to {string}")]
    public Task WhenIResetThePasswordTo(string newPassword) =>
        ResetAsync(EmailedTokens().Last(), newPassword);

    [When("I reset the password using the first emailed token")]
    public Task WhenIResetThePasswordUsingTheFirstEmailedToken() =>
        ResetAsync(EmailedTokens().First(), "brand-new-pass");

    [When("I reset the password using the token {string}")]
    public Task WhenIResetThePasswordUsingTheToken(string token) => ResetAsync(token, "brand-new-pass");

    [Then("no reset email is sent")]
    public void ThenNoResetEmailIsSent() => context.Emails.Sent.Should().BeEmpty();

    [Then("a reset email with a link is sent to {string}")]
    public void ThenAResetEmailWithALinkIsSentTo(string email)
    {
        var message = context.Emails.Sent.Should().ContainSingle().Subject;

        message.To.Should().Be(email);
        message.Body.Should().MatchRegex(@"/reset-password\?token=\S+");
    }

    [Then("the raw reset token is not stored in the database")]
    public async Task ThenTheRawResetTokenIsNotStoredInTheDatabase()
    {
        var rawToken = EmailedTokens().Single();

        await using var scope = context.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await database.PasswordResetTokens.AsNoTracking().ToListAsync();

        stored.Should().ContainSingle();
        stored[0].TokenHash.Should().NotBe(rawToken);
        stored[0].TokenHash.Should().Be(PasswordResetTokenHasher.Hash(rawToken));
    }

    [Then("the response is an invalid reset token problem details response")]
    public async Task ThenTheResponseIsAnInvalidResetTokenProblemDetailsResponse()
    {
        var response = context.Response ?? throw new InvalidOperationException("No request has been sent.");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem!.Status.Should().Be(400);
        problem.Title.Should().Be("Invalid or expired reset token");
    }

    private async Task ResetAsync(string token, string newPassword)
    {
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequest(token, newPassword));
    }

    private List<string> EmailedTokens() => context.Emails.Sent
        .Select(message => TokenPattern().Match(message.Body))
        .Where(match => match.Success)
        .Select(match => match.Groups["token"].Value)
        .ToList();
}
