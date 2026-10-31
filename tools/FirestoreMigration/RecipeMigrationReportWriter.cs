using System.Text.Json;

namespace FirestoreMigration;

public static class RecipeMigrationReportWriter
{
    public static void EnsureDistinctInputs(IReadOnlyCollection<string> paths)
    {
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Count)
            throw new FormatException("Recipe, decision, image manifest and report paths must be separate files.");
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
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await using var stream = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(json, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
