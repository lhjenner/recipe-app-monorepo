using System.Security.Cryptography;
using RecipeApp.Api.Images;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public static class RecipeImageStaging
{
    public static async Task<IReadOnlyDictionary<string, RecipeTargetImage>> StageAsync(
        RecipePreviewPlan plan, IReadOnlySet<string> approved,
        IReadOnlyList<RecipeImageValidationResult> images, IReadOnlyList<RecipeTargetRecord> targets,
        string imageRoot, IPrivateObjectMigrationStorage storage, RecipeImageJournal journal,
        CancellationToken cancellationToken, RecipeRunProgress? progress = null,
        Func<CancellationToken, Task>? checkpoint = null)
    {
        var accepted = plan.Recipes.Where(item => item.Outcome == "proposed-insert" ||
            item.Outcome == "proposed-update" && approved.Contains(item.LegacyId))
            .Select(item => item.LegacyId).ToHashSet(StringComparer.Ordinal);
        var result = new Dictionary<string, RecipeTargetImage>(StringComparer.Ordinal);
        foreach (var image in images.Where(image => accepted.Contains(image.RecipeId)))
        {
            if (image is not { Status: "validated", ByteCount: > 0, Sha256: not null,
                ContentType: not null, Width: > 0, Height: > 0 })
                throw new InvalidDataException("Only completely validated image inputs may be staged.");
            var path = RecipeImageFiles.ResolveSelectedFile(imageRoot, image.RelativePath);
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            await VerifyAsync(input, image.ByteCount!.Value, image.Sha256!, cancellationToken);
            input.Position = 0;
            var existing = targets.SingleOrDefault(target => target.LegacyId == image.RecipeId)?.Image;
            if (RecipeImagePlanning.Matches(existing, image))
            {
                result.Add(image.RecipeId, existing!);
                continue;
            }
            var entry = journal.Plan(image);
            progress?.PlannedKeys.Add(entry.ObjectKey);
            if (progress is not null) progress.JournalImages = journal.Data.Images;
            if (checkpoint is not null) await checkpoint(cancellationToken);
            await using var stored = await storage.OpenReadAsync(entry.ObjectKey, cancellationToken);
            if (stored is not null)
            {
                await VerifyAsync(stored, entry.ByteSize, entry.Sha256, cancellationToken);
                progress?.ReusedKeys.Add(entry.ObjectKey);
            }
            else
            {
                progress?.UploadAttemptedKeys.Add(entry.ObjectKey);
                if (checkpoint is not null) await checkpoint(cancellationToken);
                await storage.CreateOnlyAsync(entry.ObjectKey, input, entry.ContentType, cancellationToken);
                progress?.UploadedKeys.Add(entry.ObjectKey);
                if (checkpoint is not null) await checkpoint(cancellationToken);
                await using var uploaded = await storage.OpenReadAsync(entry.ObjectKey, cancellationToken)
                    ?? throw new InvalidDataException("Staged image could not be read back.");
                await VerifyAsync(uploaded, entry.ByteSize, entry.Sha256, cancellationToken);
            }
            progress?.VerifiedKeys.Add(entry.ObjectKey);
            journal.SetState(entry.ObjectKey, "verified");
            if (progress is not null) progress.JournalImages = journal.Data.Images;
            if (checkpoint is not null) await checkpoint(cancellationToken);
            result.Add(image.RecipeId, new(entry.ObjectKey, entry.ContentType, entry.ByteSize,
                entry.Sha256, entry.Width, entry.Height));
        }
        return result;
    }

    public static async Task VerifyAsync(Stream stream, long expectedSize, string expectedHash,
        CancellationToken cancellationToken)
    {
        if (expectedSize <= 0 || expectedSize > RecipeImageManifestValidator.MaximumBytes ||
            expectedHash is not { Length: 64 } || !expectedHash.All(Uri.IsHexDigit))
            throw new InvalidDataException("Image verification requires a bounded size and valid SHA-256.");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) != 0)
        {
            length += count;
            if (length > expectedSize)
                throw new InvalidDataException("Image bytes exceed the validated size.");
            hash.AppendData(buffer, 0, count);
        }
        if (length != expectedSize ||
            !Convert.ToHexString(hash.GetHashAndReset()).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Image size or SHA-256 differs from the validated input.");
    }

    public static void Apply(Recipe recipe, RecipeTargetImage? image)
    {
        if (image is null)
        {
            recipe.Image = null;
            return;
        }
        recipe.Image ??= new RecipeImage { RecipeId = recipe.Id, ObjectKey = image.ObjectKey,
            ContentType = image.ContentType, Sha256 = image.Sha256 };
        recipe.Image.ObjectKey = image.ObjectKey;
        recipe.Image.ContentType = image.ContentType;
        recipe.Image.ByteSize = image.ByteSize;
        recipe.Image.Sha256 = image.Sha256;
        recipe.Image.Width = image.Width;
        recipe.Image.Height = image.Height;
    }
}
