using System.Text.Json;

namespace FirestoreMigration;

public static class RecipeMigrationReportWriter
{
    public static void EnsureDistinctInputs(IReadOnlyCollection<string> paths)
    {
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Count)
            throw new FormatException("Recipe, decision, image manifest, report, journal and lock paths must be separate files.");
    }

    public static void EnsureOutsideRepository(string reportPath)
    {
        var root = new DirectoryInfo(Environment.CurrentDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, ".git")) &&
            !File.Exists(Path.Combine(root.FullName, ".git")))
            root = root.Parent;
        if (root is null) return;
        var repositoryPath = Path.GetFullPath(root.FullName) + Path.DirectorySeparatorChar;
        if (reportPath.StartsWith(repositoryPath, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Recipe report must be written outside the repository.");
    }

    public static async Task WriteAsync(string reportPath, RecipeMigrationReport report,
        CancellationToken cancellationToken, bool replace = false)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        var temporary = reportPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, reportPath, overwrite: replace);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
