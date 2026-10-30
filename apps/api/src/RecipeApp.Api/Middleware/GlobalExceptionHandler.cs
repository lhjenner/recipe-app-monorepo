using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Auth.PasswordReset;
using RecipeApp.Api.Images;

namespace RecipeApp.Api.Middleware;

public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    TimeProvider timeProvider,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            DuplicateEmailException => (StatusCodes.Status409Conflict, "Email already registered"),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized, "Invalid email or password"),
            AccountLockedException => (StatusCodes.Status423Locked, "Account temporarily locked"),
            InvalidResetTokenException => (StatusCodes.Status400BadRequest, "Invalid or expired reset token"),
            ImageStorageUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Recipe image unavailable"),
            _ => (0, string.Empty)
        };

        // Unmapped exceptions fall through to the framework's default 500 handling.
        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        if (exception is ImageStorageUnavailableException)
        {
            logger.LogError(exception, "Private recipe image retrieval failed.");
        }
        if (exception is AccountLockedException locked)
        {
            var retryAfter = Math.Max(1, (int)Math.Ceiling((locked.LockedUntilUtc - timeProvider.GetUtcNow().UtcDateTime).TotalSeconds));
            httpContext.Response.Headers.RetryAfter = retryAfter.ToString();
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = exception.Message }
        });
    }
}
