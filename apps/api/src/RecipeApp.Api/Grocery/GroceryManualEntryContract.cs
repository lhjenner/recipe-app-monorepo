using RecipeApp.Api.Ingredients;

namespace RecipeApp.Api.Grocery;

public sealed record GroceryManualEntryInput(string? Name, string? Note);

public static class GroceryManualEntryContract
{
    public static IReadOnlyDictionary<string, string[]> ValidateBatch(
        IReadOnlyList<GroceryManualEntryInput> entries)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (entries.Count is < 1 or > GroceryDecimalContract.MaximumBatchItemCount)
        {
            Add(errors, "items", $"A batch must contain between 1 and {GroceryDecimalContract.MaximumBatchItemCount} items.");
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var name = IngredientWriteValidator.Clean(entry.Name ?? string.Empty);
            if (name.Length == 0)
            {
                Add(errors, $"items[{index}].name", "Name is required.");
            }
            else if (name.Length > GroceryDecimalContract.MaximumNameLength)
            {
                Add(errors, $"items[{index}].name",
                    $"Name must be {GroceryDecimalContract.MaximumNameLength} characters or fewer.");
            }

            if (entry.Note?.Length > GroceryDecimalContract.MaximumNoteLength)
            {
                Add(errors, $"items[{index}].note",
                    $"Note must be {GroceryDecimalContract.MaximumNoteLength} characters or fewer.");
            }
        }

        return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors.Add(field, messages);
        }

        messages.Add(message);
    }
}
