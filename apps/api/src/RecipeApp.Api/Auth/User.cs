namespace RecipeApp.Api.Auth;

// The database entity — this maps to a table row via EF Core (later).
// A class (not a record) because it has identity and will be mutated
// (e.g. FailedLoginAttempts increments, LockedUntilUtc gets set).
// Guid is the "strong type" for Id per the PRD (not a primitive string).
public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; } // nullable — only set during lockout
}
