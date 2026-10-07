namespace RecipeApp.Api.Auth;

// Contract implemented by the BCrypt password hasher.
public interface IPasswordHasher
{
    string Hash(string password);
}
