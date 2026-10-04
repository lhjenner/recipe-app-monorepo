namespace RecipeApp.Api.Auth;

// Response DTO: the safe, public shape of a user. Deliberately excludes
// PasswordHash (AC-17) and internal lockout plumbing — the User entity
// is never serialized to a response.
public record UserDto(Guid Id, string Email, DateTime CreatedAtUtc);
