using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Ingredients;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryManualEntryPreparation
{
    public static async Task<PreparedManualEntries> PrepareAsync(
        AppDbContext database,
        Guid ownerId,
        GroceryManualEntryCommand command,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var selectedIds = command.Items
            .Where(item => item.IngredientId is not null)
            .Select(item => item.IngredientId!.Value)
            .Distinct()
            .ToArray();
        Dictionary<Guid, Ingredient> selectedIngredients = selectedIds.Length == 0
            ? new()
            : await database.Ingredients
                .Where(item => selectedIds.Contains(item.Id) && item.OwnerUserId == ownerId)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (selectedIds.Any(id => !selectedIngredients.ContainsKey(id)))
        {
            return PreparedManualEntries.NotFound();
        }

        var ingredientPlan = await GroceryManualIngredientPlanner.PrepareAsync(
            database, ownerId, command.Items, now, cancellationToken);
        if (ingredientPlan.Errors.Count != 0)
        {
            return PreparedManualEntries.Failure(ingredientPlan.Errors);
        }

        if (ingredientPlan.ConflictDetail is not null)
        {
            return PreparedManualEntries.Conflict(ingredientPlan.ConflictDetail);
        }

        var plannedIngredients = ingredientPlan.IngredientsByName;
        var storeIds = command.Items
            .Where(item => item.AdHocStoreId is not null)
            .Select(item => item.AdHocStoreId!.Value)
            .Distinct()
            .ToArray();
        if (storeIds.Length > 0
            && await database.Stores.CountAsync(item => storeIds.Contains(item.Id), cancellationToken) != storeIds.Length)
        {
            return PreparedManualEntries.Failure(new Dictionary<string, string[]>
            {
                ["items"] = ["One or more stores do not exist."]
            });
        }

        var adHocDepartmentIds = command.Items
            .Where(item => item.AdHocDepartmentId is not null)
            .Select(item => item.AdHocDepartmentId!.Value)
            .Distinct()
            .ToArray();
        if (adHocDepartmentIds.Length > 0
            && await database.Departments.CountAsync(
                item => adHocDepartmentIds.Contains(item.Id) && item.OwnerUserId == ownerId,
                cancellationToken) != adHocDepartmentIds.Length)
        {
            return PreparedManualEntries.Failure(new Dictionary<string, string[]>
            {
                ["items"] = ["One or more departments do not exist."]
            });
        }

        var entries = new List<PreparedManualEntry>(command.Items.Count);
        for (var index = 0; index < command.Items.Count; index++)
        {
            var input = command.Items[index];
            var interpretation = GroceryWriteValidator.InterpretPurchaseTarget(input);
            if (interpretation.Status != QuantityInterpretationStatus.Known
                || interpretation.Dimension is null
                || interpretation.CanonicalUnit is null)
            {
                Add(errors, $"items[{index}].purchaseUnit", "Purchase unit is not supported.");
                continue;
            }

            var ingredient = input.IngredientId is not null
                ? selectedIngredients[input.IngredientId.Value]
                : input.SaveAsIngredient
                    ? plannedIngredients[IngredientWriteValidator.Normalize(input.Name ?? string.Empty)]
                    : null;
            if (ingredient?.ArchivedAtUtc is not null)
            {
                Add(errors, $"items[{index}].ingredientId", "Archived Ingredients cannot be added to a grocery list.");
                continue;
            }

            if (ingredient?.ExcludeFromGroceryList == true)
            {
                Add(errors, $"items[{index}].ingredientId", "Ingredients excluded from grocery lists cannot be added.");
                continue;
            }

            var cleanName = IngredientWriteValidator.Clean(ingredient?.Name ?? input.Name ?? string.Empty);
            if (ingredient is not null
                && !string.Equals(
                    IngredientWriteValidator.Normalize(input.Name ?? ingredient.Name),
                    ingredient.NormalizedName,
                    StringComparison.Ordinal))
            {
                Add(errors, $"items[{index}].ingredientId", "The selected Ingredient changed. Select it again.");
                continue;
            }

            entries.Add(new PreparedManualEntry(
                index,
                input,
                cleanName,
                IngredientWriteValidator.Normalize(cleanName),
                ingredient?.Id,
                interpretation.Dimension.Value,
                interpretation.CountLabel,
                interpretation.CanonicalUnit));
        }

        return errors.Count == 0
            ? new PreparedManualEntries(entries, ingredientPlan.NewIngredients, [])
            : PreparedManualEntries.Failure(ToErrorArrays(errors));
    }

    private static Dictionary<string, string[]> ToErrorArrays(
        Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());

    private static void Add(
        Dictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors.Add(field, messages);
        }

        messages.Add(message);
    }
}

internal sealed record PreparedManualEntry(
    int Index,
    GroceryManualEntryRequest Input,
    string Name,
    string NormalizedName,
    Guid? IngredientId,
    QuantityDimension Dimension,
    string? CountLabel,
    string CanonicalUnit);

internal sealed record PreparedManualEntries(
    IReadOnlyList<PreparedManualEntry> Entries,
    IReadOnlyList<Ingredient> NewIngredients,
    Dictionary<string, string[]> Errors,
    bool IsNotFound = false,
    string? ConflictDetail = null)
{
    public static PreparedManualEntries Failure(Dictionary<string, string[]> errors) =>
        new([], [], errors);

    public static PreparedManualEntries NotFound() =>
        new([], [], [], IsNotFound: true);

    public static PreparedManualEntries Conflict(string detail) =>
        new([], [], [], ConflictDetail: detail);
}
