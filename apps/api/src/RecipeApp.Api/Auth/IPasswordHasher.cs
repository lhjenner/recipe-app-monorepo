namespace RecipeApp.Api.Auth;

// Contract: "anything that can hash a password."
// No implementation here — the real one (BCrypt) comes later, and the
// test's substitute is a third implementation conjured at runtime.
public interface IPasswordHasher
{
    string Hash(string password);
}
