using System.Globalization;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryCountRequirement
{
    public static ExactQuantityValue Sum(IEnumerable<GroceryContribution> contributions)
    {
        var total = new ExactQuantityValue(0, 1);
        foreach (var contribution in contributions)
        {
            var source = contribution.Origin == GroceryContributionOrigin.Recipe
                ? contribution.OriginalAmountText
                : contribution.InterpretedAmount?.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(source)
                || !ExactQuantityValue.TryParse(source.Trim(), out var amount, out _))
            {
                throw new InvalidOperationException("A known count contribution has an invalid source quantity.");
            }

            total = total.Add(amount);
        }

        return total;
    }
}
