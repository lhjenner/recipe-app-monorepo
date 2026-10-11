namespace RecipeApp.Api.Auth.PasswordReset;

// Only the SHA-256 hash of the emailed token is stored, so a database leak cannot be replayed.
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool Consumed { get; set; }
}
