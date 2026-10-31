namespace FirestoreMigration;

public sealed record RecipeMigrationSourceDisposition(string LegacyId, string? LegacyOwnerId,
    bool Selected, IReadOnlyList<RecipeMigrationFieldDisposition> Fields,
    IReadOnlyList<RecipeMigrationStepDisposition> Steps);

public sealed record RecipeMigrationFieldDisposition(string JsonPath, string Disposition);

public sealed record RecipeMigrationStepDisposition(int Position, string InstructionDisposition,
    int SourceItemCount, IReadOnlyList<RecipeMigrationItemDisposition> Items);

public sealed record RecipeMigrationItemDisposition(int Position, string JsonPath, string Disposition,
    Guid? SourceId, Guid? IngredientId);

public static class RecipeMigrationReportBuilder
{
    public static IReadOnlyList<RecipeMigrationSourceDisposition> BuildSourceDispositions(
        RecipeMigrationSource source, IReadOnlySet<string> selectedLegacyIds)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selectedLegacyIds);

        return source.Recipes.Select(recipe =>
        {
            var blankItems = source.BlankPlaceholders
                .Where(item => item.LegacyId == recipe.LegacyId)
                .ToDictionary(item => (item.StepPosition, item.ItemPosition));
            var steps = recipe.Steps.Select(step =>
            {
                var items = step.Items.Select(item => new RecipeMigrationItemDisposition(
                        item.Position, item.JsonPath, item.Kind.ToString().ToLowerInvariant(),
                        item.SourceId, item.IngredientId))
                    .Concat(blankItems.Values
                        .Where(item => item.StepPosition == step.Position)
                        .Select(item => new RecipeMigrationItemDisposition(
                            item.ItemPosition, item.JsonPath, "blank-placeholder-omitted", null, null)))
                    .OrderBy(item => item.Position)
                    .ToArray();
                return new RecipeMigrationStepDisposition(
                    step.Position, "RecipeStep.Instructions", items.Length, items);
            }).ToArray();
            return new RecipeMigrationSourceDisposition(
                recipe.LegacyId,
                recipe.LegacyOwnerId,
                selectedLegacyIds.Contains(recipe.LegacyId),
                source.FieldDispositions.Where(field => field.LegacyId == recipe.LegacyId)
                    .Select(field => new RecipeMigrationFieldDisposition(field.JsonPath, field.Disposition))
                    .ToArray(),
                steps);
        }).ToArray();
    }
}
