using System.Security.Cryptography;
using System.Text.Json;

namespace RecipeApp.Api.Grocery;

public static class GroceryCommandFingerprint
{
    public static string Compute<TRequest>(string commandType, TRequest request)
        where TRequest : notnull
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        ArgumentNullException.ThrowIfNull(request);

        var payload = JsonSerializer.SerializeToUtf8Bytes(new { commandType, request });
        return Convert.ToHexString(SHA256.HashData(payload));
    }
}
