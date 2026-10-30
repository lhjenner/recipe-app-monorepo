using System.IO.Compression;
using System.Text.Json;
using RecipeApp.Api.Images;

namespace RecipeBackup;

public static class BackupArchive
{
    public const long MaxEntryBytes = 2L * 1024 * 1024 * 1024;
    public const long MaxTotalBytes = 8L * 1024 * 1024 * 1024;

    public static async Task<string> CreateAsync(
        string output, string dump, IReadOnlyList<BackupImage> images,
        IPrivateObjectStorage storage, CancellationToken token)
    {
        output = Path.GetFullPath(output);
        if (File.Exists(output)) throw new IOException("The backup output already exists.");
        var partial = output + ".partial";
        var manifest = new BackupManifest(1, DateTimeOffset.UtcNow, new FileInfo(dump).Length,
            await VerifiedCopy.HashAsync(dump, token), images);
        manifest.Validate();
        var ownsPartial = false;
        try
        {
            await using (var file = PrivateFiles.CreateNew(partial))
            {
                ownsPartial = true;
                using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true);
                await using (var entry = zip.CreateEntry("manifest.json").Open())
                    await JsonSerializer.SerializeAsync(entry, manifest, BackupManifest.JsonOptions, token);
                await using (var source = File.OpenRead(dump))
                await using (var entry = zip.CreateEntry("database.dump", CompressionLevel.NoCompression).Open())
                    await VerifiedCopy.CopyAsync(source, entry, manifest.DatabaseBytes, manifest.DatabaseSha256, token);
                foreach (var group in images.GroupBy(BackupManifest.ObjectEntry))
                {
                    var image = group.First();
                    await using var entry = zip.CreateEntry(group.Key, CompressionLevel.NoCompression).Open();
                    await using var source = await storage.OpenReadAsync(image.ObjectKey, token)
                        ?? throw new InvalidDataException("A referenced image object is missing; backup aborted.");
                    await VerifiedCopy.CopyAsync(source, entry, image.ByteSize, image.Sha256, token);
                    // Shared bytes are archived once, but every distinct source key must be verified.
                    foreach (var other in group.Skip(1).DistinctBy(row => row.ObjectKey))
                    {
                        await using var shared = await storage.OpenReadAsync(other.ObjectKey, token)
                            ?? throw new InvalidDataException("A referenced image object is missing; backup aborted.");
                        await VerifiedCopy.CopyAsync(shared, Stream.Null, other.ByteSize, other.Sha256, token);
                    }
                }
            }
            await VerifyAsync(partial, token);
            token.ThrowIfCancellationRequested();
            File.Move(partial, output, overwrite: false);
            return output;
        }
        finally
        {
            if (ownsPartial && File.Exists(partial)) File.Delete(partial);
        }
    }

    public static async Task<BackupManifest> VerifyAsync(string path, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(path);
        return await VerifyAsync(zip, token);
    }

    internal static async Task<BackupManifest> VerifyAsync(ZipArchive zip, CancellationToken token)
    {
        if (zip.Entries.Count > 100_002 || zip.Entries.Select(entry => entry.FullName).Distinct().Count() != zip.Entries.Count)
            throw new InvalidDataException("Backup contains excessive or duplicate entries.");
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (entry.Length < 0 || entry.Length > MaxEntryBytes || total > MaxTotalBytes - entry.Length)
                throw new InvalidDataException("Backup exceeds supported size limits.");
            total += entry.Length;
        }
        var manifestEntry = zip.GetEntry("manifest.json");
        if (manifestEntry is null || manifestEntry.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Backup manifest is missing or too large.");
        BackupManifest manifest;
        try
        {
            await using var input = manifestEntry.Open();
            manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(input, BackupManifest.JsonOptions, token)
                ?? throw new InvalidDataException("Backup manifest is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Backup manifest is malformed.", exception);
        }
        manifest.Validate();
        var expected = manifest.Images.Select(BackupManifest.ObjectEntry).ToHashSet(StringComparer.Ordinal);
        expected.UnionWith(["manifest.json", "database.dump"]);
        if (!expected.SetEquals(zip.Entries.Select(entry => entry.FullName)))
            throw new InvalidDataException("Backup contains missing or unexpected entries.");
        await VerifyEntryAsync(zip.GetEntry("database.dump")!, manifest.DatabaseBytes, manifest.DatabaseSha256, token);
        foreach (var image in manifest.Images.DistinctBy(BackupManifest.ObjectEntry))
            await VerifyEntryAsync(zip.GetEntry(BackupManifest.ObjectEntry(image))!, image.ByteSize, image.Sha256, token);
        return manifest;
    }

    private static async Task VerifyEntryAsync(ZipArchiveEntry entry, long size, string hash, CancellationToken token)
    {
        if (entry.Length != size) throw new InvalidDataException("Backup entry size does not match the manifest.");
        await using var stream = entry.Open();
        await VerifiedCopy.CopyAsync(stream, Stream.Null, size, hash, token);
    }
}
