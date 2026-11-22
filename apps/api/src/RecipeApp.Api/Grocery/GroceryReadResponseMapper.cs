using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryReadResponseMapper
{
    public static GroceryItemResponse MapItem(GroceryItem item)
    {
        var currentName = item.Ingredient?.Name ?? item.NameSnapshot;
        var activeContributions = item.Contributions
            .Where(contribution => contribution.State != GroceryContributionState.Cancelled
                && (item.State != GroceryItemState.Outstanding
                    || contribution.State == GroceryContributionState.Outstanding))
            .ToArray();
        var knownContributions = activeContributions
            .Where(contribution => contribution.Dimension == item.Dimension
                && contribution.InterpretedAmount is not null)
            .ToArray();
        var requirementAmount = item.Dimension == QuantityDimension.Count
            ? GetCountRequirement(knownContributions)
            : knownContributions.Sum(contribution => contribution.InterpretedAmount!.Value);
        var requirementUnit = item.Dimension switch
        {
            QuantityDimension.Count => item.CountLabel,
            QuantityDimension.Mass => "g",
            QuantityDimension.Volume => "ml",
            _ => null
        };
        var hasRequirement = knownContributions.Length > 0;
        var storeMappings = item.Ingredient?.StoreMappings
            .Select(mapping => new GroceryStoreMappingResponse(
                mapping.StoreId,
                GetMappingState(mapping),
                mapping.DepartmentId,
                mapping.Department?.Name))
            .ToArray() ?? [];

        return new GroceryItemResponse(
            item.Id,
            item.IngredientId,
            currentName,
            ToStateName(item.State),
            item.Note,
            item.Dimension,
            item.CountLabel,
            hasRequirement ? requirementAmount : null,
            hasRequirement ? requirementUnit : null,
            item.OutstandingPurchaseQuantity,
            item.PurchaseUnit,
            item.Ingredient?.Version,
            item.StoreId,
            item.DepartmentId,
            item.Department?.Name,
            storeMappings,
            item.Contributions
                .OrderBy(contribution => contribution.CreatedAtUtc)
                .ThenBy(contribution => contribution.Id)
                .Select(contribution => new GroceryContributionResponse(
                    contribution.Id,
                    contribution.Origin,
                    ToStateName(contribution.State),
                    contribution.OriginalAmountText,
                    contribution.OriginalUnitText,
                    contribution.OriginalNotesText,
                    contribution.InterpretedAmount,
                    ToCanonicalUnit(contribution.Dimension, contribution.CountLabel),
                    contribution.UnresolvedReason,
                    contribution.SourceRecipeId,
                    contribution.SourceRecipeStepId,
                    contribution.SourceRecipeStepItemId,
                    contribution.SourceRecipeNameSnapshot))
                .ToArray());
    }

    private static decimal GetCountRequirement(GroceryContribution[] contributions)
    {
        if (!GroceryCountRequirement.Sum(contributions)
            .TryToRoundedDecimal(GroceryDecimalContract.Scale, out var amount, out _))
        {
            throw new OverflowException("The consolidated count exceeds the supported decimal range.");
        }

        return amount;
    }

    private static string? ToCanonicalUnit(QuantityDimension? dimension, string? countLabel) =>
        dimension switch
        {
            QuantityDimension.Count => countLabel,
            QuantityDimension.Mass => "g",
            QuantityDimension.Volume => "ml",
            _ => null
        };

    private static string GetMappingState(IngredientStoreMapping mapping) =>
        mapping.IsNotStocked
            ? "notStocked"
            : mapping.DepartmentId is not null
                ? "mapped"
                : "unmapped";

    private static string ToStateName(GroceryItemState state) => state switch
    {
        GroceryItemState.Outstanding => "outstanding",
        GroceryItemState.Completed => "completed",
        GroceryItemState.Cancelled => "cancelled",
        _ => throw new InvalidOperationException($"Unknown grocery item state: {state}.")
    };

    private static string ToStateName(GroceryContributionState state) => state switch
    {
        GroceryContributionState.Outstanding => "outstanding",
        GroceryContributionState.Completed => "completed",
        GroceryContributionState.Cancelled => "cancelled",
        _ => throw new InvalidOperationException($"Unknown grocery contribution state: {state}.")
    };
}
