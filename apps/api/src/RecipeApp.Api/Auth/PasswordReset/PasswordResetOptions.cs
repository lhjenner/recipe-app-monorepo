namespace RecipeApp.Api.Auth.PasswordReset;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    // Where the frontend lives; the emailed link points at {PublicBaseUrl}/reset-password.
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";
}
