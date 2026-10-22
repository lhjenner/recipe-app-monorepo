using System.Data;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed record MigrationResult(int Inserted, int Updated, int Unchanged, int Declined,
    int Headings, int Notes, int Warnings, int Errors);

public sealed class MigrationRunner(AppDbContext db, TextReader input, TextWriter output)
{
    public async Task<MigrationResult> RunAsync(MigrationOptions options, CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        output.WriteLine($"Target: server={System.Text.Json.JsonSerializer.Serialize(connection.DataSource)}, " +
            $"database={System.Text.Json.JsonSerializer.Serialize(connection.Database)}, owner={options.Owner}");
        output.WriteLine(options.Write ? "WRITE RUN: fresh preview; updates require individual approval." : "DRY RUN: no writes.");
        var files = new SourceFiles(options);
        TargetSnapshot target;
        await using (var preview = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            target = await TargetSnapshot.ReadAsync(db, options.Owner, files.Source, cancellationToken);
            await preview.CommitAsync(cancellationToken);
        }
        var plan = new MigrationPlan(files.Source, target);
        plan.Print(output);
        var unchanged = plan.Ingredients.Count(item => item.Existing is not null && item.Diffs.Count == 0);
        var warningCount = plan.Source.Warnings.Count + plan.Warnings.Count;
        var errorCount = plan.Source.Errors.Count + plan.Errors.Count + plan.Source.Unclassified.Count;
        if (!plan.CanWrite || !options.Write)
            return Finish(new(0, 0, unchanged, 0, plan.Source.Headings, plan.Source.Notes, warningCount, errorCount));
        var applied = plan.Ingredients.Where(item => item.Existing is null).Select(item => item.Incoming).ToList();
        var updated = 0;
        var declined = 0;
        foreach (var item in plan.Ingredients.Where(item => item.Diffs.Count > 0))
        {
            MigrationPlan.PrintDiff(output, item);
            output.Write("Apply this entire ingredient update? Type 'yes' to approve; anything else declines: ");
            await output.FlushAsync(cancellationToken);
            var response = await input.ReadLineAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (response?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true)
            {
                applied.Add(item.Incoming);
                updated++;
            }
            else
            {
                declined++;
            }
        }
        var conflicts = plan.NameConflicts(applied);
        if (conflicts.Count > 0)
            throw new InvalidOperationException("Approvals produce name conflicts: " + string.Join("; ", conflicts));
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        // Short, post-approval table locks prevent phantoms, child edits, and concurrent inserts.
        // All data modifications still go through EF; this SQL only acquires locks.
        await db.Database.ExecuteSqlRawAsync("""
            SET LOCAL lock_timeout = '10s';
            LOCK TABLE "Users", "__EFMigrationsHistory" IN SHARE MODE;
            LOCK TABLE "Categories", "Departments", "SupermarketChains", "Stores",
                "Ingredients", "IngredientStoreMappings", "ProductLinks" IN SHARE ROW EXCLUSIVE MODE;
            """, cancellationToken);
        using var handles = files.VerifyAndHold();
        var current = await TargetSnapshot.ReadAsync(db, options.Owner, files.Source, cancellationToken);
        if (current.Fingerprint != target.Fingerprint || !current.OwnershipConflicts.SequenceEqual(target.OwnershipConflicts))
            throw new InvalidOperationException("Target changed after preview. Nothing committed; run and review again.");
        await new MigrationWriter(db, options.Owner, current).ApplyAsync(files.Source, applied, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync(cancellationToken);
        return Finish(new(applied.Count - updated, updated, unchanged, declined,
            plan.Source.Headings, plan.Source.Notes, warningCount, 0));
    }

    private MigrationResult Finish(MigrationResult result)
    {
        output.WriteLine($"Finished: inserted={result.Inserted}, updated={result.Updated}, unchanged={result.Unchanged}, " +
            $"declined={result.Declined}, headings={result.Headings}, notes={result.Notes}, " +
            $"warnings={result.Warnings}, errors={result.Errors}");
        return result;
    }
}
