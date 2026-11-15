using System.Security.Cryptography;
using System.Text;
using RecipeApp.Api.Ingredients;

namespace RecipeApp.Api.Grocery;

public static class GroceryConsolidationKey
{
    public static string ForIngredient(
        Guid ingredientId,
        QuantityDimension dimension,
        string? countLabel) =>
        Build($"ingredient:{ingredientId:N}", dimension, countLabel);

    public static string ForName(
        string name,
        QuantityDimension dimension,
        string? countLabel)
    {
        var normalizedName = IngredientWriteValidator.Normalize(name);
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("A non-empty name is required.", nameof(name));
        }

        return Build($"name:{normalizedName}", dimension, countLabel);
    }

    public static string ForUnresolvedIngredient(Guid ingredientId, string? unitText) =>
        ForUnresolved($"ingredient:{ingredientId:N}", unitText);

    public static string ForUnresolvedName(string name, string? unitText)
    {
        var normalizedName = IngredientWriteValidator.Normalize(name);
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("A non-empty name is required.", nameof(name));
        }

        return ForUnresolved($"name:{normalizedName}", unitText);
    }

    private static string Build(
        string identity,
        QuantityDimension dimension,
        string? countLabel)
    {
        if (dimension == QuantityDimension.Count)
        {
            if (string.IsNullOrWhiteSpace(countLabel))
            {
                throw new ArgumentException("Count dimensions require a canonical count label.", nameof(countLabel));
            }

            return Hash(identity, "count", IngredientWriteValidator.Normalize(countLabel));
        }

        if (!string.IsNullOrWhiteSpace(countLabel))
        {
            throw new ArgumentException("Only count dimensions can have a count label.", nameof(countLabel));
        }

        return Hash(identity, dimension.ToString().ToLowerInvariant());
    }

    private static string ForUnresolved(string identity, string? unitText)
    {
        var normalizedUnit = IngredientWriteValidator.Normalize(unitText ?? string.Empty);
        return Hash(identity, "unresolved", normalizedUnit);
    }

    private static string Hash(params string[] components)
    {
        var canonical = new StringBuilder();
        foreach (var component in components)
        {
            canonical.Append(component.Length).Append(':').Append(component);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}
