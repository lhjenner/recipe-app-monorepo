namespace FirestoreMigration;

public static class RecipeImageFiles
{
    public static string ResolveSelectedFile(string imageRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new FormatException("Image manifest path must be relative to the selected backup directory.");
        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.None);
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." || segment.Contains(':')))
            throw new FormatException("Image manifest path contains an empty, traversal, or rooted segment.");

        var root = Path.GetFullPath(imageRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Image backup directory does not exist.");
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var candidate = root;
        for (var index = 0; index < segments.Length; index++)
        {
            candidate = Path.GetFullPath(Path.Combine(candidate, segments[index]));
            if (!candidate.StartsWith(rootPrefix, pathComparison))
                throw new FormatException("Image manifest path escapes the backup directory.");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                throw new FileNotFoundException("Manifest-selected image file does not exist.", relativePath);

            FileSystemInfo fileSystemInfo = Directory.Exists(candidate)
                ? new DirectoryInfo(candidate)
                : new FileInfo(candidate);
            if ((fileSystemInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                var resolved = fileSystemInfo.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new IOException("Unable to resolve an image path link.");
                candidate = Path.GetFullPath(resolved.FullName);
                if (!candidate.StartsWith(rootPrefix, pathComparison))
                    throw new FormatException("Image manifest path resolves outside the backup directory.");
            }
            if (index < segments.Length - 1 && !Directory.Exists(candidate))
                throw new FormatException("An image manifest path parent is not a directory.");
        }
        if (!File.Exists(candidate))
            throw new FormatException("Manifest-selected path is not a file.");
        return candidate;
    }
}
