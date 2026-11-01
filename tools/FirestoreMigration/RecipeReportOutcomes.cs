namespace FirestoreMigration;

internal static class RecipeReportOutcomes
{
    public static IReadOnlyList<RecipePreviewItem> Recipes(RecipeMigrationSource source,
        IReadOnlyList<RecipeSourceRecord> selected, RecipePreviewPlan plan, RecipeRunProgress progress,
        IReadOnlyList<string> diagnostics, IReadOnlyList<RecipeImageValidationResult> images,
        IReadOnlySet<string> ambiguousLegacyIds)
    {
        var selectedIds = selected.Select(recipe => recipe.LegacyId).ToHashSet(StringComparer.Ordinal);
        var planned = plan.Recipes.ToDictionary(item => item.LegacyId, StringComparer.Ordinal);
        var committed = progress.Results.ToDictionary(item => item.LegacyId, StringComparer.Ordinal);
        return source.Recipes.Select((recipe, index) =>
        {
            var path = $"recipes[{index}]";
            var recipeDiagnostics = diagnostics.Where(message => message.StartsWith(path, StringComparison.Ordinal) ||
                message.Contains($"LegacyId '{recipe.LegacyId}'", StringComparison.Ordinal))
                .Concat(images.Where(image => image.RecipeId == recipe.LegacyId && image.Diagnostic is not null)
                    .Select(image => image.Diagnostic!)).Distinct(StringComparer.Ordinal).ToArray();
            if (!selectedIds.Contains(recipe.LegacyId))
                return new RecipePreviewItem(recipe.LegacyId, null,
                    ambiguousLegacyIds.Contains(recipe.LegacyId) ||
                        selected.Count == 0 && progress.Status == "validation-blocked" ? "validation-blocked" : "not-selected",
                    [], SourceJsonPath: path, Diagnostics: ambiguousLegacyIds.Contains(recipe.LegacyId)
                        ? [.. recipeDiagnostics, $"{path}.userId: source-owner assignment is ambiguous."] : recipeDiagnostics);
            planned.TryGetValue(recipe.LegacyId, out var item);
            if (committed.TryGetValue(recipe.LegacyId, out var result))
                return result with { PlannedOutcome = item?.Outcome, SourceJsonPath = path, Diagnostics = recipeDiagnostics };
            var outcome = progress.Status switch
            {
                "validation-blocked" => recipeDiagnostics.Length == 0 ? "not-applied" : "validation-blocked",
                "cancelled" or "rolled-back" or "commit-uncertain" or "operational-failure" => progress.Status,
                _ => item?.Outcome ?? "not-reviewed"
            };
            if (progress.Decisions.TryGetValue(recipe.LegacyId, out var approved) && !approved)
                outcome = "declined";
            else if (item?.Outcome == "unchanged" && recipeDiagnostics.Length == 0)
                outcome = "unchanged";
            return new RecipePreviewItem(recipe.LegacyId, item?.ExistingRecipeId,
                outcome, item?.Differences ?? [], item?.Outcome, path, recipeDiagnostics);
        }).ToArray();
    }

    public static IReadOnlyList<RecipeImageWork> Images(RecipeMigrationSource source, IReadOnlyList<RecipePreviewItem> outcomes,
        IReadOnlyList<RecipeImageValidationResult> images, IReadOnlyList<RecipeTargetRecord> targets,
        RecipeRunProgress progress)
    {
        return outcomes.Select(item =>
        {
            var image = images.SingleOrDefault(image => image.RecipeId == item.LegacyId);
            var reference = source.Recipes.Single(recipe => recipe.LegacyId == item.LegacyId).ImageReference;
            var target = targets.SingleOrDefault(target => target.LegacyId == item.LegacyId)?.Image;
            var action = item.Outcome == "not-selected" ? "not-selected"
                : image is null ? !string.IsNullOrWhiteSpace(reference) ? "not-reviewed"
                    : target is null ? "none" : "remove"
                : image.Status != "validated" ? "invalid"
                : target is null ? "add"
                : RecipeImagePlanning.Matches(target, image) ? "retain" : "replace";
            var entry = progress.JournalImages.SingleOrDefault(image => image.LegacyId == item.LegacyId &&
                progress.PlannedKeys.Contains(image.ObjectKey));
            var staged = entry is not null && progress.VerifiedKeys.Contains(entry.ObjectKey);
            var outcome = item.Outcome switch
            {
                "committed-insert" or "committed-update" => action switch
                {
                    "add" or "replace" => "committed-association",
                    "remove" => "committed-removal",
                    "retain" => "retained",
                    _ => "none"
                },
                "not-selected" => "not-selected",
                "declined" => "declined",
                "unchanged" when progress.Committed => action == "retain" ? "retained" : "none",
                "commit-uncertain" when action is "add" or "replace" or "remove" => "commit-uncertain",
                "cancelled" or "rolled-back" when staged => "unreferenced-staged",
                _ when entry is not null && progress.UploadAttemptedKeys.Contains(entry.ObjectKey) &&
                    !progress.UploadedKeys.Contains(entry.ObjectKey) => "upload-uncertain",
                _ when entry is not null && progress.PlannedKeys.Contains(entry.ObjectKey) &&
                    !progress.VerifiedKeys.Contains(entry.ObjectKey) => "verification-incomplete",
                _ => action == "none" ? "none" : progress.Status is "previewing" or "preview-valid"
                    ? "planned" : "not-applied"
            };
            return new RecipeImageWork(item.LegacyId, action, outcome,
                entry?.ObjectKey ?? target?.ObjectKey, entry?.Sha256 ?? target?.Sha256,
                reference is null ? null : RecipeSourceFingerprint.Create(reference),
                image?.Sha256, action is "replace" or "remove" ? target?.ObjectKey : null);
        }).ToArray();
    }

    public static RecipeImageWorkSummary ImageSummary(IReadOnlyList<RecipeImageWork> images,
        RecipeRunProgress progress) => new(
        images.Count(image => image.PlannedAction is "add" or "replace"),
        images.Count(image => image.PlannedAction == "remove"),
        progress.UploadedKeys.Count, progress.VerifiedKeys.Count, progress.ReusedKeys.Count,
        images.Count(image => image.Outcome == "upload-uncertain"),
        images.Count(image => image.Outcome == "committed-association"),
        images.Count(image => image.Outcome == "committed-removal"),
        images.Count(image => image.Outcome == "unreferenced-staged"),
        images.Count(image => image.Outcome == "commit-uncertain"),
        images.Count(image => image.Outcome == "retained"));
}
