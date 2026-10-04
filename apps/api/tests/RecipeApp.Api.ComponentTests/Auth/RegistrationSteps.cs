using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Reqnroll;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.ComponentTests.Auth;

[Binding]
public sealed class RegistrationSteps(RegistrationApiContext context)
{
    [Given("the email {string} is available for registration")]
    public void GivenTheEmailIsAvailableForRegistration(string email)
    {
        context.Email = email;
        context.UserRepository
            .ExistsByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(false);
    }

    [Given("the email {string} is already registered")]
    public void GivenTheEmailIsAlreadyRegistered(string email)
    {
        context.Email = email;
        context.UserRepository
            .ExistsByEmailAsync(email, Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [When("I submit registration with password {string}")]
    public async Task WhenISubmitRegistrationWithPassword(string password)
    {
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(context.Email, password));
    }

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int statusCode)
    {
        Response.StatusCode.Should().Be((System.Net.HttpStatusCode)statusCode);
    }

    [Then("the response contains the user profile for {string}")]
    public async Task ThenTheResponseContainsTheUserProfileFor(string email)
    {
        var user = await Response.Content.ReadFromJsonAsync<UserDto>();

        user.Should().NotBeNull();
        user!.Email.Should().Be(email);
        user.Id.Should().NotBeEmpty();
    }

    [Then("the response is an RFC 7807 problem details response")]
    public async Task ThenTheResponseIsAProblemDetailsResponse()
    {
        Response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");

        var problem = await Response.Content.ReadFromJsonAsync<ProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Email already registered");
    }

    private HttpResponseMessage Response => context.Response
        ?? throw new InvalidOperationException("The registration request has not been sent.");
}
