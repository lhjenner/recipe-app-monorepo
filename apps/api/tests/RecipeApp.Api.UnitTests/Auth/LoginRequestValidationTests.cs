using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FluentAssertions;
using RecipeApp.Api.Auth;

namespace RecipeApp.Api.UnitTests.Auth;

public class LoginRequestValidationTests
{
    [Theory]
    [InlineData("user@example.com", "password123", true)]
    [InlineData("not-an-email", "password123", false)]
    [InlineData("", "password123", false)]
    [InlineData("user@example.com", "", false)]
    public void Validate_ReportsMissingOrMalformedFields(string email, string password, bool expectedValid)
    {
        var parameters = typeof(LoginRequest).GetConstructors().Single().GetParameters();

        var valid = IsValid(parameters[0], email) & IsValid(parameters[1], password);

        valid.Should().Be(expectedValid);
    }

    // Validation attributes sit on the record's constructor parameters, so check values against those.
    private static bool IsValid(ParameterInfo parameter, string value)
    {
        var attributes = parameter.GetCustomAttributes<ValidationAttribute>();
        return attributes.All(attribute => attribute.IsValid(value));
    }
}
