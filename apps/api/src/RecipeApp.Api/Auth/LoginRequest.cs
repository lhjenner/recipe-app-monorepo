using System.ComponentModel.DataAnnotations;

namespace RecipeApp.Api.Auth;

public record LoginRequest(
    [param: Required, EmailAddress] string Email,
    [param: Required] string Password);
