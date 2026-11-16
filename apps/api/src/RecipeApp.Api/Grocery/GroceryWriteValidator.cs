using System.Globalization;
using RecipeApp.Api.Ingredients;

namespace RecipeApp.Api.Grocery;

internal static class GroceryWriteValidator
{
    public static Dictionary<string, string[]> ValidateAdd(GroceryManualEntryCommand command)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var items = command.Items;
        if (items is null || items.Any(item => item is null))
        {
            Add(errors, "items", "At least one valid grocery item is required.");
            return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
        }

        var reviewedDuplicates = command.ReviewedDuplicates;
        if (reviewedDuplicates is null || reviewedDuplicates.Any(review => review is null))
        {
            Add(errors, nameof(command.ReviewedDuplicates), "Duplicate reviews must not contain null entries.");
            return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
        }

        foreach (var (field, messages) in GroceryManualEntryContract.ValidateBatch(
                     items.Select(item => new GroceryManualEntryInput(item.Name, item.Note)).ToArray()))
        {
            foreach (var message in messages)
            {
                Add(errors, field, message);
            }
        }

        if (command.CommandId == Guid.Empty)
        {
            Add(errors, nameof(command.CommandId), "Command ID is required.");
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var field = $"items[{index}]";
            var unitError = GroceryPurchaseQuantityContract.Validate(
                item.PurchaseQuantity,
                item.PurchaseUnit);
            if (unitError is not null)
            {
                Add(errors, $"{field}.purchaseQuantity", unitError);
            }

            if (item.IngredientId is not null && item.SaveAsIngredient)
            {
                Add(errors, $"{field}.saveAsIngredient", "An existing Ingredient cannot also be created.");
            }

            if (!item.SaveAsIngredient
                && (item.CategoryId is not null || item.StoreMappings is { Count: > 0 }))
            {
                Add(errors, $"{field}.catalogue", "Catalogue fields require saving this row as an Ingredient.");
            }

            if (item.IngredientId is not null
                && (item.AdHocStoreId is not null || item.AdHocDepartmentId is not null))
            {
                Add(errors, $"{field}.store", "Ad-hoc store fields cannot be used with a linked Ingredient.");
            }

            if (item.AdHocDepartmentId is not null && item.AdHocStoreId is null)
            {
                Add(errors, $"{field}.adHocDepartmentId", "A store is required for an ad-hoc department.");
            }

            var storeMappings = item.StoreMappings ?? [];
            var ingredientRequest = new IngredientWriteRequest
            {
                Name = item.Name ?? string.Empty,
                CategoryId = item.CategoryId,
                StoreMappings = storeMappings
            };
            foreach (var (mappingField, messages) in IngredientWriteValidator.Validate(ingredientRequest))
            {
                foreach (var message in messages)
                {
                    Add(errors, $"{field}.{mappingField}", message);
                }
            }
        }

        return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }

    public static Dictionary<string, string[]> ValidateUpdate(
        GroceryItemUpdateRequest request,
        string currentPurchaseUnit)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (request.CommandId == Guid.Empty)
        {
            Add(errors, nameof(request.CommandId), "Command ID is required.");
        }

        if (request.Note?.Length > GroceryDecimalContract.MaximumNoteLength)
        {
            Add(errors, nameof(request.Note),
                $"Note must be {GroceryDecimalContract.MaximumNoteLength} characters or fewer.");
        }

        if (request.AdHocDepartmentId is not null && request.AdHocStoreId is null)
        {
            Add(errors, nameof(request.AdHocDepartmentId), "A store is required for an ad-hoc department.");
        }

        if (!string.Equals(request.PurchaseUnit?.Trim(), currentPurchaseUnit, StringComparison.OrdinalIgnoreCase))
        {
            Add(errors, nameof(request.PurchaseUnit), "The purchase unit cannot be changed for an existing line.");
        }
        else
        {
            var unitError = GroceryPurchaseQuantityContract.Validate(
                request.OutstandingPurchaseQuantity,
                currentPurchaseUnit);
            if (unitError is not null)
            {
                Add(errors, nameof(request.OutstandingPurchaseQuantity), unitError);
            }
        }

        return errors.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }

    public static QuantityInterpretation InterpretPurchaseTarget(GroceryManualEntryRequest request) =>
        QuantityInterpreter.Interpret(
            request.PurchaseQuantity.ToString(CultureInfo.InvariantCulture),
            request.PurchaseUnit);

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
