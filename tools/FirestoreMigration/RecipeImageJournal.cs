using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirestoreMigration;

public sealed record JournalImage(string LegacyId, string ObjectKey, string ContentType,
    long ByteSize, string Sha256, int Width, int Height, string State);

public sealed record ImageJournalData(int SchemaVersion, Guid RunId, Guid OwnerId,
    string StorageScope, string DatabaseScope, string InputFingerprint, bool CommitAttempted, bool Committed,
    IReadOnlyList<JournalImage> Images);

public sealed class RecipeImageJournal : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };
    private readonly FileStream _lock;
    public string Path { get; }
    public ImageJournalData Data { get; private set; }

    private RecipeImageJournal(string path, FileStream fileLock, ImageJournalData data)
    {
        Path = path;
        _lock = fileLock;
        Data = data;
    }

    public static RecipeImageJournal Open(string path, Guid owner, string scope, string databaseScope, string fingerprint)
    {
        var journal = Load(path, new(1, Guid.CreateVersion7(), owner, scope, databaseScope, fingerprint, false, false, []));
        if (journal.Data.OwnerId != owner || journal.Data.StorageScope != scope ||
            journal.Data.DatabaseScope != databaseScope || journal.Data.InputFingerprint != fingerprint)
        {
            journal.Dispose();
            throw new InvalidOperationException("Image journal belongs to different inputs, owner or storage. Choose a new journal.");
        }
        return journal;
    }

    public static RecipeImageJournal OpenExisting(string path) => Load(path, null);

    private static RecipeImageJournal Load(string path, ImageJournalData? initial)
    {
        path = System.IO.Path.GetFullPath(path);
        RecipeMigrationReportWriter.EnsureOutsideRepository(path);
        if (!Directory.Exists(System.IO.Path.GetDirectoryName(path)))
            throw new DirectoryNotFoundException("Image journal directory does not exist.");
        var fileLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            var data = File.Exists(path)
                ? JsonSerializer.Deserialize<ImageJournalData>(File.ReadAllBytes(path), JsonOptions)
                    ?? throw new InvalidDataException("Image journal is empty.")
                : initial ?? throw new FileNotFoundException("Image journal does not exist.");
            Validate(data);
            var journal = new RecipeImageJournal(path, fileLock, data);
            if (!File.Exists(path)) journal.Save(data);
            return journal;
        }
        catch
        {
            fileLock.Dispose();
            throw;
        }
    }

    public void Save(ImageJournalData data)
    {
        Validate(data);
        var temporary = Path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions));
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, Path, overwrite: true);
            Data = data;
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public JournalImage Plan(RecipeImageValidationResult image)
    {
        var existing = Data.Images.SingleOrDefault(entry => entry.LegacyId == image.RecipeId);
        if (existing is not null)
        {
            if (existing.Sha256 != image.Sha256 || existing.ByteSize != image.ByteCount ||
                existing.ContentType != image.ContentType || existing.Width != image.Width ||
                existing.Height != image.Height || existing.State == "deleted")
                throw new InvalidDataException("Journal image does not match validated input or was explicitly deleted.");
            return existing;
        }
        var entry = new JournalImage(image.RecipeId,
            $"recipes/imports/{Data.OwnerId:N}/{Data.RunId:N}/{Guid.NewGuid():N}",
            image.ContentType ?? throw new InvalidDataException("Image has no validated content type."),
            image.ByteCount ?? throw new InvalidDataException("Image has no validated size."),
            image.Sha256 ?? throw new InvalidDataException("Image has no validated checksum."),
            image.Width ?? throw new InvalidDataException("Image has no validated width."),
            image.Height ?? throw new InvalidDataException("Image has no validated height."), "planned");
        Save(Data with { Images = [.. Data.Images, entry] });
        return entry;
    }

    public void SetState(string key, string state)
    {
        if (!Data.Images.Any(image => image.ObjectKey == key))
            throw new InvalidOperationException("Cannot update an object key absent from the staging journal.");
        Save(Data with { Images = Data.Images.Select(image =>
            image.ObjectKey == key ? image with { State = state } : image).ToArray() });
    }

    private static void Validate(ImageJournalData data)
    {
        if (data.SchemaVersion != 1 || data.RunId == Guid.Empty || data.OwnerId == Guid.Empty ||
            string.IsNullOrWhiteSpace(data.StorageScope) || !HashValid(data.DatabaseScope) || !HashValid(data.InputFingerprint) ||
            data.Images is null || data.Images.Any(image => image is null) ||
            data.Images.Select(image => image.LegacyId).Distinct(StringComparer.Ordinal).Count() != data.Images.Count)
            throw new InvalidDataException("Invalid image journal header or duplicate Recipe entries.");
        var prefix = $"recipes/imports/{data.OwnerId:N}/{data.RunId:N}/";
        foreach (var image in data.Images)
        {
            if (string.IsNullOrWhiteSpace(image.LegacyId) || image.ObjectKey is null ||
                !image.ObjectKey.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(image.ObjectKey[prefix.Length..], "N", out _) ||
                !HashValid(image.Sha256) || image.ByteSize <= 0 ||
                image.ByteSize > RecipeImageManifestValidator.MaximumBytes ||
                image.Width <= 0 || image.Height <= 0 ||
                image.Width > RecipeImageManifestValidator.MaximumDimension ||
                image.Height > RecipeImageManifestValidator.MaximumDimension ||
                (long)image.Width * image.Height > RecipeImageManifestValidator.MaximumPixels ||
                image.ContentType is not ("image/png" or "image/jpeg" or "image/webp") ||
                image.State is not ("planned" or "verified" or "referenced" or "unreferenced" or "missing" or "deleted"))
                throw new InvalidDataException("Invalid or out-of-scope journal image; no storage operations were performed.");
        }
        if (data.Images.Select(image => image.ObjectKey).Distinct(StringComparer.Ordinal).Count() != data.Images.Count)
            throw new InvalidDataException("Duplicate journal object keys.");
    }

    private static bool HashValid(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    public void Dispose() => _lock.Dispose();
}
