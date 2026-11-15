using System.Globalization;

namespace RecipeApp.Api.Grocery;

public static class GroceryPurchaseQuantityContract
{
    public static string? Validate(decimal quantity, string? unit)
    {
        if (quantity <= 0)
        {
            return "Quantity must be greater than zero.";
        }

        if (quantity > GroceryDecimalContract.MaximumEnteredPurchaseQuantity)
        {
            return "Quantity cannot exceed 9,999.";
        }

        if (decimal.Round(quantity, GroceryDecimalContract.Scale) != quantity)
        {
            return $"Quantity cannot have more than {GroceryDecimalContract.Scale} decimal places.";
        }

        var parsedUnit = QuantityInterpreter.Interpret(
            quantity.ToString(CultureInfo.InvariantCulture),
            unit);
        if (parsedUnit.Status != QuantityInterpretationStatus.Known)
        {
            return "Purchase unit is not supported.";
        }

        if (parsedUnit.Dimension == QuantityDimension.Count)
        {
            return decimal.Truncate(quantity) == quantity
                ? null
                : "Count purchase quantities must be whole numbers.";
        }

        var canonicalInputUnit = (unit ?? string.Empty).Trim().ToLowerInvariant();
        return canonicalInputUnit switch
        {
            "g" or "gram" or "grams" or "ml" or "millilitre" or "millilitres" => null,
            _ => "Measured purchase units must be grams or millilitres."
        };
    }
}
