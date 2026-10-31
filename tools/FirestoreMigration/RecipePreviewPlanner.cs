namespace FirestoreMigration;

public sealed record RecipeTargetItem(int Position, string Kind, Guid? IngredientId, string Text,
    string? QuantityText, string? UnitText, string? NotesText);

public sealed record RecipeTargetStep(int Position, string Instructions, IReadOnlyList<RecipeTargetItem> Items);

public sealed record RecipeTargetRecord(Guid Id, string LegacyId, Guid OwnerUserId, string Name,
    string? SourceText, string? SourceUrl, string? ServingsText, string? PrepTimeText, string? CookTimeText,
    DateTime? DateAddedAtUtc, bool IsArchived, IReadOnlyList<string> Tags,
    IReadOnlyList<RecipeTargetStep> Steps, string? SourceDocumentJson);

public sealed record RecipePreviewItem(string LegacyId, Guid? ExistingRecipeId, string Outcome,
    IReadOnlyList<string> Differences);

public sealed record RecipePreviewPlan(IReadOnlyList<RecipePreviewItem> Recipes,
    IReadOnlyList<string> BlockingDiagnostics)
{
    public int ProposedInserts => Recipes.Count(recipe => recipe.Outcome == "proposed-insert");
    public int ProposedUpdates => Recipes.Count(recipe => recipe.Outcome == "proposed-update");
    public int Unchanged => Recipes.Count(recipe => recipe.Outcome == "unchanged");
}

public static class RecipePreviewPlanner
{
    public static RecipePreviewPlan Create(RecipeSourceValidationResult validation, Guid destinationOwnerId,
        IEnumerable<RecipeTargetRecord> targetRecipes)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(targetRecipes);
        var diagnostics = new List<string>(validation.BlockingDiagnostics);
        var targetByLegacyId = new Dictionary<string, RecipeTargetRecord>(StringComparer.Ordinal);
        foreach (var target in targetRecipes)
        {
            if (target.OwnerUserId != destinationOwnerId)
            {
                diagnostics.Add($"Target Recipe {target.Id} belongs to a different owner; it was excluded from matching.");
                continue;
            }
            if (!targetByLegacyId.TryAdd(target.LegacyId, target))
                diagnostics.Add($"Target owner has duplicate Recipe LegacyId '{target.LegacyId}'.");
        }

        var plans = new List<RecipePreviewItem>();
        foreach (var incoming in validation.Recipes)
        {
            if (!targetByLegacyId.TryGetValue(incoming.LegacyId, out var existing))
            {
                plans.Add(new(incoming.LegacyId, null, "proposed-insert", ["new Recipe"]));
                continue;
            }

            var differences = Compare(incoming, existing);
            plans.Add(new(incoming.LegacyId, existing.Id,
                differences.Count == 0 ? "unchanged" : "proposed-update", differences));
        }
        return new(plans, diagnostics);
    }

    private static IReadOnlyList<string> Compare(RecipeSourceRecord incoming, RecipeTargetRecord existing)
    {
        var differences = new List<string>();
        AddIfDifferent(differences, "Name", incoming.Name, existing.Name);
        AddIfDifferent(differences, "SourceText", incoming.SourceText, existing.SourceText);
        AddIfDifferent(differences, "SourceUrl", incoming.SourceUrl, existing.SourceUrl);
        AddIfDifferent(differences, "ServingsText", incoming.ServingsText, existing.ServingsText);
        AddIfDifferent(differences, "PrepTimeText", incoming.PrepTimeText, existing.PrepTimeText);
        AddIfDifferent(differences, "CookTimeText", incoming.CookTimeText, existing.CookTimeText);
        AddIfDifferent(differences, "DateAddedAtUtc", incoming.DateAddedAtUtc, existing.DateAddedAtUtc);
        AddIfDifferent(differences, "IsArchived", incoming.IsArchived, existing.IsArchived);
        if (!incoming.Tags.SequenceEqual(existing.Tags, StringComparer.Ordinal))
            differences.Add("Tags");
        if (!StepsEqual(incoming.Steps, existing.Steps))
            differences.Add("Steps");
        if (!JsonDocumentsEqual(incoming.SourceDocumentJson, existing.SourceDocumentJson))
            differences.Add("SourceProvenance");
        return differences;
    }

    private static bool StepsEqual(IReadOnlyList<RecipeSourceStep> source, IReadOnlyList<RecipeTargetStep> target)
    {
        if (source.Count != target.Count) return false;
        for (var stepIndex = 0; stepIndex < source.Count; stepIndex++)
        {
            var sourceStep = source[stepIndex];
            var targetStep = target[stepIndex];
            if (sourceStep.Position != targetStep.Position ||
                !string.Equals(sourceStep.Instructions, targetStep.Instructions, StringComparison.Ordinal) ||
                sourceStep.Items.Count != targetStep.Items.Count)
                return false;

            for (var itemIndex = 0; itemIndex < sourceStep.Items.Count; itemIndex++)
            {
                var sourceItem = sourceStep.Items[itemIndex];
                var targetItem = targetStep.Items[itemIndex];
                if (sourceItem.Position != targetItem.Position ||
                    !string.Equals(sourceItem.Kind.ToString(), targetItem.Kind, StringComparison.Ordinal) ||
                    sourceItem.IngredientId != targetItem.IngredientId ||
                    !string.Equals(sourceItem.Text, targetItem.Text, StringComparison.Ordinal) ||
                    !string.Equals(sourceItem.QuantityText, targetItem.QuantityText, StringComparison.Ordinal) ||
                    !string.Equals(sourceItem.UnitText, targetItem.UnitText, StringComparison.Ordinal) ||
                    !string.Equals(sourceItem.NotesText, targetItem.NotesText, StringComparison.Ordinal))
                    return false;
            }
        }
        return true;
    }

    private static bool JsonDocumentsEqual(string? left, string? right)
    {
        if (left is null || right is null) return left is null && right is null;
        try
        {
            using var leftDocument = System.Text.Json.JsonDocument.Parse(left);
            using var rightDocument = System.Text.Json.JsonDocument.Parse(right);
            return CanonicalJson(leftDocument.RootElement) == CanonicalJson(rightDocument.RootElement);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string CanonicalJson(System.Text.Json.JsonElement element) =>
        element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => "{" + string.Join(",",
                element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
                    .Select(property => System.Text.Json.JsonSerializer.Serialize(property.Name) + ":" +
                        CanonicalJson(property.Value))) + "}",
            System.Text.Json.JsonValueKind.Array => "[" + string.Join(",",
                element.EnumerateArray().Select(CanonicalJson)) + "]",
            _ => element.GetRawText()
        };

    private static void AddIfDifferent<T>(ICollection<string> differences, string field, T source, T target)
    {
        if (!EqualityComparer<T>.Default.Equals(source, target))
            differences.Add(field);
    }
}
