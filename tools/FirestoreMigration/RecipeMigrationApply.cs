using RecipeApp.Api.Images;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed record RecipeApplyResult(Guid RunId, int Inserted, int Updated,
    IReadOnlyList<RecipePreviewItem> Recipes, IReadOnlyList<JournalImage> JournalImages);

internal static class RecipeMigrationApply
{
    public static async Task<RecipeApplyResult> ApplyAsync(AppDbContext db, MigrationOptions options,
        RecipePreviewPlan plan, IReadOnlyList<RecipeSourceRecord> source,
        IReadOnlyList<RecipeTargetRecord> targets, IReadOnlyList<RecipeImageValidationResult> images,
        RecipeSourceFiles files, IReadOnlyList<Guid> sourceIds, string targetFingerprint,
        string sourceHash, string decisionFingerprint, string inputFingerprint, Guid runId,
        IPrivateObjectMigrationStorage? storage, TextReader input, TextWriter output,
        CancellationToken cancellationToken)
    {
        var approved = await RecipeUpdateApproval.CollectAsync(plan, source, targets,
            input, output, cancellationToken);
        RecipeImageJournal? journal = null;
        var committed = false;
        try
        {
            IReadOnlyDictionary<string, RecipeTargetImage>? staged = null;
            if (images.Count > 0)
            {
                if (storage is null || options.ImageJournalFile is null || options.ImageRoot is null)
                    throw new InvalidOperationException("Image writes require private storage, --image-journal and --image-root.");
                journal = RecipeImageJournal.Open(options.ImageJournalFile, options.Owner, storage.Scope,
                    RecipeImageRecovery.DatabaseScope(db), inputFingerprint);
                runId = journal.Data.RunId;
                using (files.VerifyAndHold())
                {
                    await RecipeImagePlanning.VerifyUnchangedAsync(images, targets, storage, cancellationToken);
                    staged = await RecipeImageStaging.StageAsync(plan, approved, images, targets,
                        options.ImageRoot, storage, journal, cancellationToken);
                }
                journal.Save(journal.Data with { CommitAttempted = true });
                output.WriteLine($"Image staging: run={runId}, journal={journal.Path}. Reconcile this journal before cleanup.");
            }
            var result = await new RecipeImportWriter(db).ApplyAsync(plan, source, approved,
                options, runId, sourceHash, decisionFingerprint, targetFingerprint, files,
                sourceIds, cancellationToken, staged);
            committed = true;
            if (journal is not null)
            {
                var referencedKeys = result.Recipes.Select(item =>
                    item.Outcome is "committed-insert" or "committed-update"
                        ? staged?.GetValueOrDefault(item.LegacyId)?.ObjectKey
                        : targets.SingleOrDefault(target => target.LegacyId == item.LegacyId)?.Image?.ObjectKey)
                    .Where(key => key is not null).ToHashSet(StringComparer.Ordinal);
                journal.Save(journal.Data with
                {
                    Committed = true,
                    Images = journal.Data.Images.Select(image => image with
                    {
                        State = referencedKeys.Contains(image.ObjectKey) ? "referenced" : "unreferenced"
                    }).ToArray()
                });
            }
            foreach (var target in targets.Where(target => target.Image is not null &&
                result.Recipes.Any(item => item.LegacyId == target.LegacyId && item.Outcome == "committed-update")))
                output.WriteLine($"Previous image for Recipe '{target.LegacyId}' was retained in storage; no automatic cleanup.");
            return new(runId, result.Inserted, result.Updated, result.Recipes, journal?.Data.Images ?? []);
        }
        catch
        {
            output.WriteLine($"Recipe run {runId}: {(committed ? "database commit succeeded; journal finalization failed" : "no success claimed; reconcile database state before cleanup")}.");
            if (journal is not null) output.WriteLine($"Recovery journal: {journal.Path}; staged objects were not automatically deleted.");
            throw;
        }
        finally
        {
            journal?.Dispose();
        }
    }
}
