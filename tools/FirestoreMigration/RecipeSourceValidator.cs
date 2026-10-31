namespace FirestoreMigration;

public sealed record RecipeIngredientTarget(Guid IngredientId, Guid OwnerUserId,
    bool IsArchived = false, bool ExcludeFromGroceryList = false);

public sealed record RecipeSourceValidationResult(
    IReadOnlyList<RecipeSourceRecord> Recipes,
    IReadOnlyList<string> BlockingDiagnostics,
    int MissingIngredientReferences = 0,
    int CrossOwnerIngredientReferences = 0)
{
    public bool CanContinue => BlockingDiagnostics.Count == 0;
}

public static class RecipeSourceValidator
{
    public static RecipeSourceValidationResult Validate(RecipeMigrationSource source, Guid destinationOwnerId,
        IEnumerable<RecipeIngredientTarget> targetIngredients,
        IReadOnlySet<string>? selectedLegacyOwnerIds = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targetIngredients);
        var diagnostics = new List<string>(source.BlockingDiagnostics);
        if (destinationOwnerId == Guid.Empty)
            diagnostics.Add("Destination owner must be a non-empty GUID.");

        var availableOwners = source.LegacyOwnerIds;
        if (availableOwners.Count > 1 && selectedLegacyOwnerIds is null)
        {
            diagnostics.Add("Multiple legacy owner IDs occur in the export; explicit source-owner selection is required.");
            return new([], diagnostics);
        }

        IReadOnlyList<RecipeSourceRecord> selectedRecipes;
        if (selectedLegacyOwnerIds is null)
        {
            selectedRecipes = source.Recipes;
        }
        else
        {
            var unknownSelections = selectedLegacyOwnerIds.Except(availableOwners, StringComparer.Ordinal).ToArray();
            foreach (var ownerId in unknownSelections)
                diagnostics.Add($"Selected legacy owner '{ownerId}' is not present in the export.");
            if (selectedLegacyOwnerIds.Count == 0)
                diagnostics.Add("At least one legacy owner must be selected.");
            if (availableOwners.Count > 1 &&
                source.Recipes.Any(recipe => recipe.LegacyOwnerId is null))
                diagnostics.Add("Recipes with missing legacy owner IDs cannot be assigned from a multi-owner export.");
            selectedRecipes = source.Recipes
                .Where(recipe => recipe.LegacyOwnerId is null
                    ? availableOwners.Count == 0
                    : selectedLegacyOwnerIds.Contains(recipe.LegacyOwnerId))
                .ToArray();
            if (selectedRecipes.Count == 0 && source.Recipes.Count > 0)
                diagnostics.Add("The selected source-owner set contains no Recipes.");
        }

        var ingredientRows = targetIngredients
            .GroupBy(ingredient => ingredient.IngredientId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var missingIngredientReferences = 0;
        var crossOwnerIngredientReferences = 0;
        foreach (var duplicate in ingredientRows.Where(entry => entry.Value.Length > 1))
            diagnostics.Add($"Target Ingredient snapshot contains duplicate ID {duplicate.Key}.");

        foreach (var recipe in selectedRecipes)
        foreach (var item in recipe.Steps.SelectMany(step => step.Items))
        {
            if (item.Kind is RecipeSourceItemKind.Heading or RecipeSourceItemKind.Note)
            {
                if (ingredientRows.ContainsKey(item.SourceId))
                    diagnostics.Add($"{item.JsonPath}: source ID {item.SourceId} is classified as {item.Kind} " +
                        "but conflicts with an existing Ingredient row.");
                continue;
            }

            if (!item.IngredientId.HasValue)
            {
                diagnostics.Add($"{item.JsonPath}.id: Ingredient item has no resolved GUID.");
                continue;
            }

            var id = item.IngredientId.Value;
            if (!ingredientRows.TryGetValue(id, out var matches))
            {
                missingIngredientReferences++;
                diagnostics.Add($"{item.JsonPath}: Ingredient {id} does not exist in the target database.");
                continue;
            }
            if (matches.All(ingredient => ingredient.OwnerUserId != destinationOwnerId))
            {
                crossOwnerIngredientReferences++;
                diagnostics.Add($"{item.JsonPath}: Ingredient {id} belongs to a different owner.");
            }
        }

        return new(selectedRecipes, diagnostics, missingIngredientReferences,
            crossOwnerIngredientReferences);
    }
}
