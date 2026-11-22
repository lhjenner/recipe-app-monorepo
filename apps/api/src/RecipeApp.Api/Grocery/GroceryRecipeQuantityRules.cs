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
                var countContributions = recipeContributions
                    .Where(contribution => contribution.Dimension == QuantityDimension.Count
                        && contribution.InterpretedAmount is not null
                        && string.Equals(contribution.CountLabel, item.CountLabel, StringComparison.Ordinal));
                if (!GroceryCountRequirement.Sum(countContributions).TryCeiling(out recipeDemand))
                {
                    outstandingQuantity = 0;
                    return false;
                }
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
