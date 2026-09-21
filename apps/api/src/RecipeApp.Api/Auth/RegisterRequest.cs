namespace RecipeApp.Api.Auth;

// Request DTO: the shape of data arriving from the client when registering.
// A positional record — the two properties are declared right in the parentheses.
public record RegisterRequest(string Email, string Password);
