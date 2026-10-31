using RecipeApp.Api.Images;

namespace FirestoreMigration;

public static class RecipeImagePlanning
{
    public static RecipePreviewPlan IncludeImages(RecipePreviewPlan plan,
        IReadOnlyList<RecipeImageValidationResult> images, IReadOnlyList<RecipeTargetRecord> targets)
    {
        var incoming = images.ToDictionary(image => image.RecipeId, StringComparer.Ordinal);
        var existing = targets.ToDictionary(target => target.LegacyId, StringComparer.Ordinal);
        return plan with { Recipes = plan.Recipes.Select(item =>
        {
            if (!existing.TryGetValue(item.LegacyId, out var target)) return item;
            incoming.TryGetValue(item.LegacyId, out var image);
            if (image is null ? target.Image is null : Matches(target.Image, image)) return item;
            return item with { Outcome = "proposed-update", Differences = [.. item.Differences, "Image"] };
        }).ToArray() };
    }

    public static bool Matches(RecipeTargetImage? target, RecipeImageValidationResult incoming) =>
        target is not null && incoming.Status == "validated" &&
        target.Sha256.Equals(incoming.Sha256, StringComparison.OrdinalIgnoreCase) &&
        target.ByteSize == incoming.ByteCount && target.ContentType == incoming.ContentType &&
        target.Width == incoming.Width && target.Height == incoming.Height;

    public static async Task VerifyUnchangedAsync(
        IReadOnlyList<RecipeImageValidationResult> images, IReadOnlyList<RecipeTargetRecord> targets,
        IPrivateObjectMigrationStorage storage, CancellationToken cancellationToken)
    {
        foreach (var target in targets.Where(target => target.Image is not null))
        {
            var image = images.SingleOrDefault(image => image.RecipeId == target.LegacyId);
            if (image is null || !Matches(target.Image, image)) continue;
            await using var stored = await storage.OpenReadAsync(target.Image!.ObjectKey, cancellationToken)
                ?? throw new InvalidDataException("An unchanged target image is missing; no Recipes were committed.");
            await RecipeImageStaging.VerifyAsync(stored, target.Image.ByteSize,
                target.Image.Sha256, cancellationToken);
        }
    }
}
