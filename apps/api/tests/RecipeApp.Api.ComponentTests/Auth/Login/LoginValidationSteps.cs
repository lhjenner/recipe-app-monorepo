using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Reqnroll;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.ComponentTests.Auth.Login;

[Binding]
public sealed class LoginValidationSteps(ApiAuthContext context)
{
    [When("I submit a login request with email {string} and password {string}")]
    public async Task WhenISubmitALoginRequestWithEmailAndPassword(string email, string password)
    {
        context.Response = await context.Client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password));
    }

    [When("I submit a login request with an empty JSON body")]
    public async Task WhenISubmitALoginRequestWithAnEmptyJsonBody()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        context.Response = await context.Client.PostAsync("/api/auth/login", content);
    }

    [Then("the response contains a validation error for {string}")]
    public async Task ThenTheResponseContainsAValidationErrorFor(string field)
    {
        var response = context.Response
            ?? throw new InvalidOperationException("The login request has not been sent.");

        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        problem.Should().NotBeNull();
        problem!.Errors.Should().ContainKey(field);
    }
}
