using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryRecipeQuantityRules
{
    public static bool TryRecalculate(GroceryItem item, out decimal outstandingQuantity)
    {
        try
        {
            var contributions = item.Contributions
                .Where(contribution => contribution.State == GroceryContributionState.Outstanding)
                .ToArray();
            var recipeContributions = contributions
                .Where(contribution => contribution.Origin == GroceryContributionOrigin.Recipe)
                .ToArray();
            var manualDemand = contributions
                .Where(contribution => contribution.Origin == GroceryContributionOrigin.Manual)
                .Sum(contribution => contribution.ManualPurchaseIncrement ?? 0m);

            decimal recipeDemand;
            if (item.Dimension == QuantityDimension.Count)
            {
                var requiredCount = recipeContributions
                    .Where(contribution => contribution.Dimension == QuantityDimension.Count
                        && string.Equals(contribution.CountLabel, item.CountLabel, StringComparison.Ordinal))
                    .Sum(contribution => contribution.InterpretedAmount ?? 0m);
                recipeDemand = decimal.Ceiling(requiredCount);
            }
            else
            {
                var hasRecipeSuggestion = recipeContributions.Length > 0;
                recipeDemand = hasRecipeSuggestion ? 1m : 0m;
            }

            outstandingQuantity = checked(recipeDemand + manualDemand);
            return GroceryDecimalContract.CanRepresent(outstandingQuantity);
        }
        catch (OverflowException)
        {
            outstandingQuantity = 0;
            return false;
        }
    }

}
