using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.ComponentTests.Auth.Login;

[Binding]
public sealed class LockoutSteps(ApiAuthContext context)
{
    [When("I submit {int} login requests with an invalid password")]
    public async Task WhenISubmitInvalidLoginRequests(int count)
    {
        for (var attempt = 0; attempt < count; attempt++)
        {
            context.Response?.Dispose();
            context.Response = await context.Client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(context.Email, "invalid-password"));
            context.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    [When("{int} minutes pass")]
    public void WhenMinutesPass(int minutes) => context.Time.Advance(TimeSpan.FromMinutes(minutes));

    [Then("the response is a lockout problem details response")]
    public async Task ThenTheResponseIsALockoutProblemDetailsResponse()
    {
        Response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await Response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(423);
        problem.Title.Should().Be("Account temporarily locked");
    }

    [Then("the response tells the client to retry in {int} seconds")]
    public void ThenTheResponseTellsTheClientToRetryInSeconds(int seconds)
    {
        Response.Headers.RetryAfter?.Delta.Should().Be(TimeSpan.FromSeconds(seconds));
    }

    [Then("no session cookie is issued")]
    public void ThenNoSessionCookieIsIssued()
    {
        var setCookies = Response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

        setCookies.Should().NotContain(value => value.StartsWith("RecipeApp.Session=", StringComparison.Ordinal));
    }

    [Then("the stored failed login counter is {int}")]
    public async Task ThenTheStoredFailedLoginCounterIs(int expected)
    {
        var user = await context.FindUserAsync(context.Email);

        user.Should().NotBeNull();
        user!.FailedLoginAttempts.Should().Be(expected);
        user.LockedUntilUtc.Should().BeNull();
    }

    private HttpResponseMessage Response => context.Response
        ?? throw new InvalidOperationException("No request has been sent.");
}
