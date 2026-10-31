using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;

namespace FirestoreMigration;

public sealed record RecipeImageManifestEntry(string RecipeId, string ImageReference,
    string RelativePath, long ByteCount, string Sha256);

public sealed record RecipeImageValidationResult(string RecipeId, string ImageReference,
    string RelativePath, string? ContentType, long? ByteCount, string? Sha256,
    int? Width, int? Height, string Status, string? Diagnostic);

public sealed class RecipeImageManifestResult
{
    public List<RecipeImageValidationResult> Images { get; } = [];
    public List<string> BlockingDiagnostics { get; } = [];
    public List<string> Warnings { get; } = [];
}

public static class RecipeImageManifestParser
{
    public static IReadOnlyList<RecipeImageManifestEntry> Parse(string json)
    {
        using var document = JsonInput.Read(json, JsonValueKind.Array, "image-manifest.json");
        var entries = new List<RecipeImageManifestEntry>();
        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var path = $"image-manifest[{index}]";
            JsonInput.Require(element, JsonValueKind.Object, path);
            var recipeId = RequiredText(element, "recipeId", path);
            var imageReference = RequiredText(element, "imageReference", path);
            var relativePath = RequiredText(element, "path", path);
            if (!element.TryGetProperty("byteCount", out var byteCount) ||
                byteCount.ValueKind != JsonValueKind.Number || !byteCount.TryGetInt64(out var bytes) || bytes <= 0)
                throw new FormatException($"{path}.byteCount: expected a positive integer.");
            var sha256 = RequiredText(element, "sha256", path);
            if (sha256.Length != 64 || !sha256.All(Uri.IsHexDigit))
                throw new FormatException($"{path}.sha256: expected 64 hexadecimal characters.");
            if (!recipeIds.Add(recipeId))
                throw new FormatException($"{path}.recipeId: duplicate Recipe image manifest entry '{recipeId}'.");

            var extraName = element.EnumerateObject()
                .Select(property => property.Name)
                .FirstOrDefault(name => name is not
                    ("recipeId" or "imageReference" or "path" or "byteCount" or "sha256"));
            if (extraName is not null)
                throw new FormatException($"{path}.{extraName}: unknown manifest field.");
            entries.Add(new(recipeId, imageReference, relativePath, bytes, sha256.ToUpperInvariant()));
            index++;
        }
        return entries;
    }

    private static string RequiredText(JsonElement element, string key, string path)
    {
        if (!element.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
            throw new FormatException($"{path}.{key}: expected a non-empty string.");
        return value.GetString()!;
    }
}

public static class RecipeImageManifestValidator
{
    public const long MaximumBytes = 20 * 1024 * 1024;
    public const int MaximumDimension = 10_000;
    public const long MaximumPixels = 40_000_000;

    public static RecipeImageManifestResult Validate(IEnumerable<RecipeSourceRecord> recipes,
        IReadOnlyList<RecipeImageManifestEntry>? manifest, string? imageRoot)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        var result = new RecipeImageManifestResult();
        var recipeList = recipes.ToArray();
        var recipesWithImages = recipeList
            .Where(recipe => !string.IsNullOrWhiteSpace(recipe.ImageReference))
            .ToArray();
        if (recipesWithImages.Length > 0 && (manifest is null || imageRoot is null))
        {
            foreach (var recipe in recipesWithImages)
                AddFailure(result, recipe.LegacyId, recipe.ImageReference!, "",
                    "A referenced image requires an explicit image manifest and backup directory.");
            return result;
        }

        var entries = manifest ?? [];
        var entriesByRecipe = entries.ToDictionary(entry => entry.RecipeId, StringComparer.Ordinal);
        var matchedRecipeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in recipesWithImages)
        {
            if (!entriesByRecipe.TryGetValue(recipe.LegacyId, out var entry))
            {
                AddFailure(result, recipe.LegacyId, recipe.ImageReference!, "",
                    "No image manifest entry exists for the Recipe.");
                continue;
            }
            if (!string.Equals(entry.ImageReference, recipe.ImageReference, StringComparison.Ordinal))
            {
                AddFailure(result, recipe.LegacyId, recipe.ImageReference!, entry.RelativePath,
                    "Manifest image reference does not exactly match the source Recipe.");
                continue;
            }
            matchedRecipeIds.Add(recipe.LegacyId);
            ValidateEntry(entry, imageRoot!, result);
        }

        foreach (var entry in entries.Where(entry => !matchedRecipeIds.Contains(entry.RecipeId)))
            result.Warnings.Add($"Manifest entry for Recipe '{entry.RecipeId}' is not referenced by the selected source set; its file was not opened.");
        return result;
    }

    private static void ValidateEntry(RecipeImageManifestEntry entry, string imageRoot,
        RecipeImageManifestResult result)
    {
        string path;
        try
        {
            path = ResolveSelectedFile(imageRoot, entry.RelativePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            ArgumentException or FormatException)
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath, exception.Message);
            return;
        }

        long fileLength;
        try
        {
            fileLength = new FileInfo(path).Length;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                $"Unable to inspect manifest-selected image file ({exception.GetType().Name}).");
            return;
        }
        if (fileLength <= 0 || fileLength > MaximumBytes)
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                $"Image size must be between 1 byte and {MaximumBytes} bytes.");
            return;
        }
        if (fileLength != entry.ByteCount)
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                $"Manifest byte count {entry.ByteCount} does not match file size {fileLength}.");
            return;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                $"Unable to read manifest-selected image file ({exception.GetType().Name}).");
            return;
        }
        if (bytes.Length != fileLength || bytes.Length > MaximumBytes)
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                "Image file changed while being read or exceeded the size limit.");
            return;
        }
        var checksum = Convert.ToHexString(SHA256.HashData(bytes));
        if (!checksum.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                "Image SHA-256 does not match the manifest.");
            return;
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var codec = SKCodec.Create(stream, out var codecResult);
            if (codec is null || codecResult != SKCodecResult.Success)
            {
                AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                    "Image signature is unsupported or the decoder could not identify the file.");
                return;
            }

            var format = ContentType(codec.EncodedFormat);
            if (format is null)
            {
                AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                    "Only PNG, JPEG, and WebP images are supported.");
                return;
            }

            var imageInfo = codec.Info;
            var width = imageInfo.Width;
            var height = imageInfo.Height;
            if (width <= 0 || height <= 0 ||
                width > MaximumDimension || height > MaximumDimension ||
                (long)width * height > MaximumPixels)
            {
                AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                    $"Image dimensions must be at most {MaximumDimension}x{MaximumDimension} and {MaximumPixels} total pixels.",
                    format, bytes.Length, checksum, width, height);
                return;
            }

            using var bitmap = new SKBitmap(imageInfo);
            var decodeResult = codec.GetPixels(imageInfo, bitmap.GetPixels());
            if (decodeResult != SKCodecResult.Success)
            {
                AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                    $"Image decoding failed ({decodeResult}).", format, bytes.Length, checksum, width, height);
                return;
            }

            result.Images.Add(new(entry.RecipeId, entry.ImageReference, entry.RelativePath,
                format, bytes.Length, checksum, width, height, "validated", null));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            AddFailure(result, entry.RecipeId, entry.ImageReference, entry.RelativePath,
                $"Image decoding failed ({exception.GetType().Name}).");
        }
    }

    private static string ResolveSelectedFile(string imageRoot, string relativePath)
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

    private static string? ContentType(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Png => "image/png",
        SKEncodedImageFormat.Jpeg => "image/jpeg",
        SKEncodedImageFormat.Webp => "image/webp",
        _ => null
    };

    private static void AddFailure(RecipeImageManifestResult result, string recipeId, string reference,
        string relativePath, string diagnostic, string? contentType = null, long? byteCount = null,
        string? sha256 = null, int? width = null, int? height = null)
    {
        var message = $"Recipe '{recipeId}', image '{relativePath}': {diagnostic}";
        result.BlockingDiagnostics.Add(message);
        result.Images.Add(new(recipeId, reference, relativePath, contentType, byteCount,
            sha256, width, height, "invalid", diagnostic));
    }
}
