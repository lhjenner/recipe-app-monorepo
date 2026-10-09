using Reqnroll;
using RecipeApp.Api.Auth;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;

namespace RecipeApp.Api.ComponentTests.Auth.Login;

[Binding]
public class LoginSteps (ApiAuthContext context)
{
    [Given("a registered account with email {string} and password {string}")]
    public async Task GivenARegisteredAccountWithEmailAndPassword(string email, string password)
    {
        context.Email = email;
        context.Password = password;
        await context.SeedExistingAccountAsync(email, password);
    }
      
    [Given ("an unregistered email address {string} and password {string}")]
    public void GivenAnUnregisteredEmailAddressAndPassword(string email, string password)
    {
        context.Email = email;
        context.Password = password;
    }

    [Given ("a logged in user")]
    public async Task GivenALoggedInUser()
    {
        context.Email = "logged_in_user@example.com";
        context.Password = "password";
        await context.SeedExistingAccountAsync(context.Email, context.Password);

        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(context.Email, context.Password));
    }
      
    [When("I submit a login request")]
    public async Task WhenISubmitALoginRequest()
    {
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(context.Email, context.Password));
    }

    [When("I submit a login request with an invalid password")]
    public async Task WhenISubmitALoginRequestWithAnInvalidPassword()
    {
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(context.Email, "invalid-password"));
    }

    [When ("I log out")]
    public async Task WhenILogOut()
    {
        context.Response = await context.Client.PostAsync("/api/auth/logout", null);
    }

    [Then("login is successful")]
    public async Task ThenLoginIsSuccessful()
    {
        Response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var user = await Response.Content.ReadFromJsonAsync<UserDto>();

        user.Should().NotBeNull();
        user!.Email.Should().Be(context.Email);
        user.Id.Should().NotBeEmpty();
    }

    [Then("the session cookie is HttpOnly, Secure, and SameSite")]
    public void ThenTheSessionCookieIsHttpOnlySecureAndSameSite()
    {
        var sessionCookie = Response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("RecipeApp.Session=", StringComparison.Ordinal));
        var cookieAttributes = sessionCookie.ToLowerInvariant();
        context.OriginalSessionCookie = sessionCookie.Split(';', 2)[0];

        cookieAttributes.Should().Contain("httponly");
        cookieAttributes.Should().Contain("secure");
        cookieAttributes.Should().Contain("samesite=lax");
    }

    [When("I request my current profile")]
    public async Task WhenIRequestMyCurrentProfile()
    {
        context.Response = await context.Client.GetAsync("/api/auth/me");
    }

    [Then("the current profile is returned for {string}")]
    public async Task ThenTheCurrentProfileIsReturnedFor(string email)
    {
        Response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var user = await Response.Content.ReadFromJsonAsync<UserDto>();

        user.Should().NotBeNull();
        user!.Email.Should().Be(email);
    }
    
    [Then("the response informs of invalid credentials")]
    public async Task ThenTheResponseInformsOfInvalidCredentials()
    {
        Response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");

        var problem = await Response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(401);
        problem.Title.Should().Be("Invalid email or password");
    }

    [Then ("logout is successful")]
    public async Task ThenLogoutIsSuccessful()
    {
        Response.StatusCode.Should().Be(System.Net.HttpStatusCode.NoContent);
    }

    [Then("the session cookie is cleared")]
    public void ThenTheSessionCookieIsCleared()
    {
        var sessionCookie = Response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("RecipeApp.Session=", StringComparison.Ordinal));

        sessionCookie.Split(';', 2)[0].Should().Be("RecipeApp.Session=");
        sessionCookie.ToLowerInvariant().Should().Contain("expires=");
    }

    [When("I retry the original session cookie")]
    public async Task WhenIRetryTheOriginalSessionCookie()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", context.OriginalSessionCookie);
        context.Response = await context.Client.SendAsync(request);
    }

    [Then("the request is unauthorized")]
    public void ThenTheRequestIsUnauthorized()
    {
        Response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    private HttpResponseMessage Response => context.Response
        ?? throw new InvalidOperationException("The login request has not been sent.");
}
