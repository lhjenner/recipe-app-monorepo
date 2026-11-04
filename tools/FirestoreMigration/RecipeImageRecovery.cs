using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Images;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed record ImageRecoveryResult(Guid RunId, bool Committed, int Referenced,
    int Unreferenced, int Missing, int Deleted);

public static class RecipeImageRecovery
{
    public static async Task<ImageRecoveryResult> RecoverAsync(AppDbContext db,
        IPrivateObjectMigrationStorage storage, RecipeImageJournal journal, bool cleanup,
        CancellationToken cancellationToken = default)
    {
        if (journal.Data.StorageScope != storage.Scope || journal.Data.DatabaseScope != DatabaseScope(db))
            throw new InvalidOperationException("Recovery database or storage does not match the journal target.");
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any() || db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Recovery database schema is not current.");
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            SET LOCAL lock_timeout = '10s';
            LOCK TABLE "Recipes", "RecipeImages", "RecipeImportProvenance" IN SHARE ROW EXCLUSIVE MODE;
            """, cancellationToken);
        var keys = journal.Data.Images.Select(image => image.ObjectKey).ToArray();
        var references = await db.RecipeImages.AsNoTracking().Where(image => keys.Contains(image.ObjectKey))
            .ToListAsync(cancellationToken);
        var runId = journal.Data.RunId;
        var committed = await db.RecipeImportProvenance.AsNoTracking()
            .AnyAsync(row => row.LastAppliedRunId == runId, cancellationToken);
        foreach (var image in journal.Data.Images.ToArray())
        {
            var referenced = references.Where(reference => reference.ObjectKey == image.ObjectKey).ToArray();
            if (referenced.Any(reference => reference.Sha256 != image.Sha256 ||
                reference.ByteSize != image.ByteSize || reference.ContentType != image.ContentType ||
                reference.Width != image.Width || reference.Height != image.Height))
                throw new InvalidDataException("A journal key has incompatible committed metadata; cleanup is prohibited.");
            await using var stored = await storage.OpenReadAsync(image.ObjectKey, cancellationToken);
            if (stored is null)
            {
                if (referenced.Length > 0)
                    throw new InvalidDataException("A committed image is missing; recovery cannot claim success.");
                journal.SetState(image.ObjectKey, image.State == "deleted" ? "deleted" : "missing");
                continue;
            }
            await RecipeImageStaging.VerifyAsync(stored, image.ByteSize, image.Sha256, cancellationToken);
            journal.SetState(image.ObjectKey, referenced.Length > 0 ? "referenced" : "unreferenced");
            if (cleanup && referenced.Length == 0)
            {
                await stored.DisposeAsync();
                await storage.DeleteAsync(image.ObjectKey, cancellationToken);
                journal.SetState(image.ObjectKey, "deleted");
            }
        }
        journal.Save(journal.Data with { Committed = journal.Data.Committed || committed || references.Count > 0 });
        await transaction.CommitAsync(cancellationToken);
        return new(runId, journal.Data.Committed,
            journal.Data.Images.Count(image => image.State == "referenced"),
            journal.Data.Images.Count(image => image.State == "unreferenced"),
            journal.Data.Images.Count(image => image.State == "missing"),
            journal.Data.Images.Count(image => image.State == "deleted"));
    }

    public static string DatabaseScope(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{connection.DataSource}|{connection.Database}")));
    }
}
