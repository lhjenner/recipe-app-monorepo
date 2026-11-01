using System.Text.Json;
using FirestoreMigration;
using RecipeApp.TestFixtures;

namespace RecipeApp.Api.ComponentTests.Migration;

public sealed partial class MigrationFixture
{
    public async Task<RecipeMigrationRunResult> RunRecipeAsync(string recipeJson, bool write,
        string reportPath, string? selectedLegacyOwnerId = null, bool includeImageManifest = false,
        bool corruptImage = false, string? ownerConfirmation = null, string? updateApproval = "yes",
        Func<Task>? onUpdateApproval = null, CancellationToken cancellationToken = default)
    {
        File.WriteAllText(RecipeFile, recipeJson);
        File.WriteAllText(DecisionFile, DecisionContents ?? "{}");
        var args = new List<string>
        {
            "--mode", "recipes", "--recipes", RecipeFile, "--owner", Owner.ToString(),
            "--decisions", DecisionFile, "--report", reportPath
        };
        if (write) args.Add("--write");
        if (ImageJournalPath is not null)
        {
            args.Add("--image-journal");
            args.Add(ImageJournalPath);
        }
        if (includeImageManifest)
        {
            Directory.CreateDirectory(ImageRoot);
            var imageBytes = corruptImage ? "not an image"u8.ToArray() : ImageBytes ?? SyntheticImage.Png;
            File.WriteAllBytes(Path.Combine(ImageRoot, "synthetic.png"), imageBytes);
            var manifest = new[]
            {
                new
                {
                    recipeId = "synthetic-recipe",
                    imageReference = "https://example.test/synthetic",
                    path = "synthetic.png",
                    byteCount = imageBytes.LongLength,
                    sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(imageBytes))
                }
            };
            File.WriteAllText(ImageManifestFile, JsonSerializer.Serialize(manifest));
            args.Add("--image-manifest");
            args.Add(ImageManifestFile);
            args.Add("--image-root");
            args.Add(ImageRoot);
        }
        if (selectedLegacyOwnerId is not null)
        {
            args.Add("--source-owner");
            args.Add(selectedLegacyOwnerId);
        }
        var options = MigrationOptions.Parse(args.ToArray());
        await using var db = Database(RecipeInterceptor);
        var ownerResponse = ownerConfirmation ?? Owner.ToString();
        using TextReader confirmation = onUpdateApproval is null
            ? new StringReader(updateApproval is null
                ? ownerResponse
                : $"{ownerResponse}{Environment.NewLine}{updateApproval}")
            : new RecipeApprovalReader(ownerResponse, onUpdateApproval, updateApproval);
        return await new RecipeMigrationRunner(db, Output, confirmation, ImageStorage, ReportPublisher)
            .RunAsync(options, cancellationToken);
    }
}
