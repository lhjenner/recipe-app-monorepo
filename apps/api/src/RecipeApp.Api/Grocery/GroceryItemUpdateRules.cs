using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryItemUpdateRules
{
    public static async Task<GroceryWriteOutcome?> ApplyDepartmentUpdateAsync(
        AppDbContext database,
        GroceryItem item,
        GroceryDepartmentUpdate update,
        Guid ownerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (item.IngredientId is null)
        {
            return Invalid("Only a linked Ingredient has a master department.", "departmentUpdate");
        }

        if (!await database.Stores.AnyAsync(value => value.Id == update.StoreId, cancellationToken))
        {
            return Invalid("The selected supermarket does not exist.", "departmentUpdate.storeId");
        }

        if (update.DepartmentId is not null
            && !await database.Departments.AnyAsync(value =>
                value.Id == update.DepartmentId && value.OwnerUserId == ownerId, cancellationToken))
        {
            return Invalid("The selected department does not exist.", "departmentUpdate.departmentId");
        }

        var ingredient = await database.Ingredients
            .Include(value => value.StoreMappings)
            .SingleOrDefaultAsync(value => value.Id == item.IngredientId && value.OwnerUserId == ownerId, cancellationToken);
        if (ingredient is null)
        {
            return new GroceryWriteOutcome(null, StatusCodes.Status404NotFound);
        }

        if (ingredient.Version != update.ExpectedIngredientVersion)
        {
            return new GroceryWriteOutcome(
                null,
                StatusCodes.Status409Conflict,
                "Conflict",
                "The Ingredient changed while you reviewed its department. Reload and review it again.");
        }

        var mapping = ingredient.StoreMappings.SingleOrDefault(value => value.StoreId == update.StoreId);
        if (update.DepartmentId is null)
        {
            if (mapping is { IsNotStocked: false })
            {
                database.IngredientStoreMappings.Remove(mapping);
            }
        }
        else if (mapping is null)
        {
            database.IngredientStoreMappings.Add(new IngredientStoreMapping
            {
                IngredientId = ingredient.Id,
                StoreId = update.StoreId,
                DepartmentId = update.DepartmentId,
                IsNotStocked = false
            });
        }
        else
        {
            mapping.DepartmentId = update.DepartmentId;
            mapping.IsNotStocked = false;
        }

        ingredient.UpdatedAtUtc = now;
        database.Entry(ingredient).Property(value => value.UpdatedAtUtc).IsModified = true;
        database.Entry(ingredient).Property(value => value.Version).OriginalValue = update.ExpectedIngredientVersion;
        return null;
    }

    public static decimal GetRecipePurchaseFloor(GroceryItem item)
    {
        var outstandingManualDemand = item.Contributions
            .Where(value => value.Origin == GroceryContributionOrigin.Manual
                && value.State == GroceryContributionState.Outstanding
                && value.ManualPurchaseIncrement is not null)
            .Sum(value => value.ManualPurchaseIncrement!.Value);
        return Math.Max(0, item.OutstandingPurchaseQuantity - outstandingManualDemand);
    }

    public static void AdjustManualDemand(
        GroceryItem item,
        decimal desiredManualAmount,
        DateTime now)
    {
        var manualContributions = item.Contributions
            .Where(value => value.Origin == GroceryContributionOrigin.Manual
                && value.State == GroceryContributionState.Outstanding
                && value.ManualPurchaseIncrement is not null)
            .OrderBy(value => value.CreatedAtUtc)
            .ThenBy(value => value.Id)
            .ToArray();
        var remaining = desiredManualAmount;
        foreach (var contribution in manualContributions)
        {
            var retained = Math.Min(contribution.ManualPurchaseIncrement!.Value, remaining);
            contribution.ManualPurchaseIncrement = retained;
            if (retained == 0)
            {
                contribution.State = GroceryContributionState.Cancelled;
            }
            else
            {
                remaining -= retained;
            }
        }

        if (remaining > 0)
        {
            item.Contributions.Add(new GroceryContribution
            {
                GroceryListId = item.GroceryListId,
                GroceryItemId = item.Id,
                OwnerUserId = item.OwnerUserId,
                Origin = GroceryContributionOrigin.Manual,
                OriginalNameText = item.NameSnapshot,
                OriginalAmountText = remaining.ToString(CultureInfo.InvariantCulture),
                OriginalUnitText = item.PurchaseUnit,
                Dimension = item.Dimension,
                CountLabel = item.CountLabel,
                ManualPurchaseIncrement = remaining,
                State = GroceryContributionState.Outstanding,
                CreatedAtUtc = now
            });
        }
    }

    private static GroceryWriteOutcome Invalid(string message, string field) =>
        new(null, StatusCodes.Status400BadRequest, "Validation failed",
            Errors: new Dictionary<string, string[]> { [field] = [message] });
}
