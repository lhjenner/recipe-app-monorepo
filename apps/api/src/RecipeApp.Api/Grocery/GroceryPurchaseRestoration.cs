using System.Text.Json;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryPurchaseRestoration
{
    public static bool TryRestore(GroceryItem original, GroceryItem target, GroceryPurchaseEvent purchase, DateTime now)
    {
        var ids = JsonSerializer.Deserialize<Guid[]>(purchase.AffectedContributionIdsJson) ?? [];
        var affected = original.Contributions
            .Where(value => ids.Contains(value.Id) && value.State == GroceryContributionState.Completed)
            .ToArray();
        var restoredManual = affected.Where(value => value.Origin == GroceryContributionOrigin.Manual)
            .Sum(value => value.ManualPurchaseIncrement ?? 0m);
        if (restoredManual > purchase.Quantity
            || !GroceryDecimalContract.CanRepresent(target.OutstandingPurchaseQuantity + purchase.Quantity))
        {
            return false;
        }

        foreach (var contribution in affected)
        {
            contribution.State = GroceryContributionState.Outstanding;
            contribution.GroceryItemId = target.Id;
            if (contribution.Origin == GroceryContributionOrigin.Recipe)
            {
                contribution.IsRestoredRecipeAssociation = true;
            }
        }

        target.OutstandingPurchaseQuantity += purchase.Quantity;
        target.RestoredPurchaseQuantity += purchase.Quantity - restoredManual;
        target.State = GroceryItemState.Outstanding;
        target.UpdatedAtUtc = now;
        return true;
    }
}
