using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipeBackup;

public sealed record BackupImage(
    Guid RecipeId, Guid OwnerUserId, string ObjectKey, string ContentType,
    long ByteSize, string Sha256, int Width, int Height);

public sealed record BackupManifest(
    int Version, DateTimeOffset CreatedAtUtc, long DatabaseBytes, string DatabaseSha256,
    IReadOnlyList<BackupImage> Images)
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public void Validate()
    {
        if (Version != 1 || CreatedAtUtc == default || DatabaseBytes <= 0
            || DatabaseBytes > BackupArchive.MaxEntryBytes || !IsHash(DatabaseSha256)
            || Images is null || Images.Count > 100_000)
            throw new InvalidDataException("Invalid backup manifest header.");
        var recipes = new HashSet<Guid>();
        var keys = new Dictionary<string, (long, string)>(StringComparer.Ordinal);
        foreach (var image in Images)
        {
            if (image is null || image.RecipeId == Guid.Empty || image.OwnerUserId == Guid.Empty
                || !recipes.Add(image.RecipeId) || string.IsNullOrWhiteSpace(image.ObjectKey)
                || image.ObjectKey.Length > 1024 || string.IsNullOrWhiteSpace(image.ContentType)
                || image.ContentType.Length > 255 || image.ByteSize <= 0
                || image.ByteSize > BackupArchive.MaxEntryBytes || !IsHash(image.Sha256)
                || image.Width <= 0 || image.Height <= 0)
                throw new InvalidDataException("Invalid or duplicate image association in backup manifest.");
            var value = (image.ByteSize, image.Sha256);
            if (keys.TryGetValue(image.ObjectKey, out var previous) && previous != value)
                throw new InvalidDataException("An object key has conflicting verification metadata.");
            keys[image.ObjectKey] = value;
        }
        foreach (var group in Images.GroupBy(image => image.Sha256))
            if (group.Select(image => image.ByteSize).Distinct().Count() != 1)
                throw new InvalidDataException("A checksum has conflicting byte sizes.");
        long total = DatabaseBytes;
        foreach (var image in Images.DistinctBy(image => image.Sha256))
        {
            if (total > BackupArchive.MaxTotalBytes - image.ByteSize)
                throw new InvalidDataException("Backup exceeds the supported total size.");
            total += image.ByteSize;
        }
    }

    internal static bool IsHash(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static string ObjectEntry(BackupImage image) => $"objects/{image.Sha256}.bin";
}
