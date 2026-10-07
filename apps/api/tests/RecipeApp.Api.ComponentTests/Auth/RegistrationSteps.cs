using System.Net.Http.Json;
using BCrypt.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
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
    }

    [Given("the email {string} is already registered")]
    public async Task GivenTheEmailIsAlreadyRegistered(string email)
    {
        context.Email = email;
        await context.SeedExistingAccountAsync(email);
    }

    [When("I submit registration with password {string}")]
    public async Task WhenISubmitRegistrationWithPassword(string password)
    {
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(context.Email, password));
    }

    [When("I submit registration with email {string} and password {string}")]
    public async Task WhenISubmitRegistrationWithEmailAndPassword(string email, string password)
    {
        context.Email = email;
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, password));
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

    [Then("the stored password is a BCrypt hash for {string}")]
    public async Task ThenTheStoredPasswordIsABCryptHashFor(string password)
    {
        var user = await context.FindUserAsync(context.Email);

        user.Should().NotBeNull();
        user!.PasswordHash.Should().NotBe(password);
        BCrypt.Net.BCrypt.Verify(password, user.PasswordHash).Should().BeTrue();
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

    [Then("the response contains validation errors for {string} and {string}")]
    public async Task ThenTheResponseContainsValidationErrors(string firstField, string secondField)
    {
        Response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");

        var problem = await Response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey(firstField);
        problem.Errors.Should().ContainKey(secondField);
    }

    private HttpResponseMessage Response => context.Response
        ?? throw new InvalidOperationException("The registration request has not been sent.");
}
