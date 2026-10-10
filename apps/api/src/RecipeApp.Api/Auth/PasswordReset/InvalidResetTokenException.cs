namespace RecipeApp.Api.Auth.PasswordReset;

public sealed class InvalidResetTokenException()
    : Exception("The password reset link is invalid or has expired.");
