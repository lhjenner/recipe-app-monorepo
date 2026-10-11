using System.Security.Cryptography;
using System.Text;

namespace RecipeApp.Api.Auth.PasswordReset;

public static class PasswordResetTokenHasher
{
    // 256 bits of randomness, URL-safe so it can sit in a query string unescaped.
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // A fast hash is appropriate here: the input is high-entropy, not a human-chosen password.
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
