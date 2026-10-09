namespace RecipeApp.Api.Auth;

public sealed class InvalidCredentialsException()
    : Exception("Invalid email or password");
