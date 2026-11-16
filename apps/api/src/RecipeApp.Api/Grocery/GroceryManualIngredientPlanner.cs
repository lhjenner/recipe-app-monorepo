using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Ingredients;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryManualIngredientPlanner
{
    public static async Task<ManualIngredientPlan> PrepareAsync(
        AppDbContext database,
        Guid ownerId,
        IReadOnlyList<GroceryManualEntryRequest> entries,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<string>();
        var plannedIngredients = new Dictionary<string, Ingredient>(StringComparer.Ordinal);
        var creationRows = entries
            .Select((item, index) => (item, index))
            .Where(pair => pair.item.SaveAsIngredient)
            .GroupBy(pair => IngredientWriteValidator.Normalize(pair.item.Name ?? string.Empty));

        foreach (var group in creationRows)
        {
            var first = group.First().item;
            var normalizedName = group.Key;
            if (group.Any(pair => !SameCatalogueValues(first, pair.item)))
            {
                foreach (var pair in group)
                {
                    Add(errors, $"items[{pair.index}].catalogue",
                        "Rows with the same Ingredient name must use the same Category and store departments.");
                }

                continue;
            }

            var writeRequest = ToIngredientRequest(first);
            foreach (var (field, messages) in IngredientWriteValidator.Validate(writeRequest))
            {
                foreach (var message in messages)
                {
                    Add(errors, $"items[{group.First().index}].{field}", message);
                }
            }

            var referenceErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            await IngredientWritePersistence.ValidateReferencesAsync(
                database, ownerId, writeRequest, referenceErrors, cancellationToken);
            foreach (var (field, messages) in referenceErrors)
            {
                foreach (var message in messages)
                {
                    Add(errors, $"items[{group.First().index}].{field}", message);
                }
            }

            var conflict = await database.Ingredients
                .Where(item => item.OwnerUserId == ownerId && item.NormalizedName == normalizedName)
                .Select(item => new { item.ArchivedAtUtc })
                .SingleOrDefaultAsync(cancellationToken);
            if (conflict is not null)
            {
                conflicts.Add(conflict.ArchivedAtUtc is null
                    ? $"An Ingredient named \"{first.Name}\" already exists. Select it from the Ingredient lookup."
                    : $"An archived Ingredient named \"{first.Name}\" exists. Restore it from Ingredient Maintenance before adding it.");
                continue;
            }

            plannedIngredients[normalizedName] = new Ingredient
            {
                Id = Guid.CreateVersion7(),
                OwnerUserId = ownerId,
                Name = IngredientWriteValidator.Clean(first.Name ?? string.Empty),
                NormalizedName = normalizedName,
                CategoryId = first.CategoryId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }

        return new ManualIngredientPlan(
            plannedIngredients,
            plannedIngredients.Values.ToArray(),
            ToErrorArrays(errors),
            conflicts.Count == 0 ? null : string.Join(" ", conflicts));
    }

    private static IngredientWriteRequest ToIngredientRequest(GroceryManualEntryRequest input) =>
        new()
        {
            Name = input.Name ?? string.Empty,
            CategoryId = input.CategoryId,
            StoreMappings = input.StoreMappings ?? []
        };

    private static bool SameCatalogueValues(
        GroceryManualEntryRequest left,
        GroceryManualEntryRequest right) =>
        left.CategoryId == right.CategoryId
        && (left.StoreMappings ?? [])
            .OrderBy(mapping => mapping.StoreId)
            .SequenceEqual((right.StoreMappings ?? []).OrderBy(mapping => mapping.StoreId));

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

internal sealed record ManualIngredientPlan(
    IReadOnlyDictionary<string, Ingredient> IngredientsByName,
    IReadOnlyList<Ingredient> NewIngredients,
    Dictionary<string, string[]> Errors,
    string? ConflictDetail);
