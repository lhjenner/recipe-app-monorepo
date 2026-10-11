using System.ComponentModel.DataAnnotations;

namespace RecipeApp.Api.Auth.PasswordReset;

public record ForgotPasswordRequest([param: Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [param: Required] string Token,
    [param: Required] string NewPassword);
