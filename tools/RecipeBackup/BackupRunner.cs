using System.Data;
using System.IO.Compression;
using Amazon.S3;
using Amazon.S3.Model;
using Npgsql;
using RecipeApp.Api.Images;

namespace RecipeBackup;

public sealed class BackupRunner(PostgresTools tools)
{
    public async Task<BackupManifest> ExportAsync(
        string connectionString, IPrivateObjectStorage storage, string output, CancellationToken token = default)
    {
        var dump = Path.Combine(Path.GetTempPath(), $"recipe-backup-{Guid.NewGuid():N}.dump");
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, token);
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
                await readOnly.ExecuteNonQueryAsync(token);
            await using var snapshotCommand = new NpgsqlCommand("SELECT pg_export_snapshot()", connection, transaction);
            var snapshot = (string)(await snapshotCommand.ExecuteScalarAsync(token))!;
            var images = await BackupDatabase.ImagesAsync(connection, transaction, token);
            await tools.DumpAsync(connectionString, snapshot, dump, token);
            await transaction.CommitAsync(token);
            await BackupArchive.CreateAsync(output, dump, images, storage, token);
            return await BackupArchive.VerifyAsync(output, token);
        }
        finally
        {
            if (File.Exists(dump)) File.Delete(dump);
        }
    }

    public async Task RestoreAsync(
        string connectionString, IAmazonS3 client, string bucket, IPrivateObjectStorage storage,
        string archive, CancellationToken token = default)
    {
        // Verify all archive bytes before contacting or changing the destination.
        using var archiveFile = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(archiveFile, ZipArchiveMode.Read);
        var manifest = await BackupArchive.VerifyAsync(zip, token);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await BackupDatabase.RequireEmptyAsync(connection, token);
        var objects = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucket, MaxKeys = 1
        }, token);
        if (objects.S3Objects is { Count: > 0 } || objects.KeyCount != 0 || objects.IsTruncated != false)
            throw new InvalidOperationException("Restore requires an empty destination bucket, explicitly confirmed by the service.");

        var dump = Path.Combine(Path.GetTempPath(), $"recipe-restore-{Guid.NewGuid():N}.dump");
        var objectFile = Path.Combine(Path.GetTempPath(), $"recipe-restore-{Guid.NewGuid():N}.object");
        try
        {
            await using (var input = zip.GetEntry("database.dump")!.Open())
            await using (var output = PrivateFiles.CreateNew(dump))
                await VerifiedCopy.CopyAsync(input, output, manifest.DatabaseBytes, manifest.DatabaseSha256, token);
            foreach (var image in manifest.Images.DistinctBy(row => row.ObjectKey))
            {
                // S3 signing requires a known length; ZIP entry streams are not seekable.
                await using (var staged = PrivateFiles.CreateNew(objectFile))
                {
                    await using (var input = zip.GetEntry(BackupManifest.ObjectEntry(image))!.Open())
                        await VerifiedCopy.CopyAsync(input, staged, image.ByteSize, image.Sha256, token);
                    staged.Position = 0;
                    await storage.WriteAsync(image.ObjectKey, staged, token);
                }
                File.Delete(objectFile);
                await using var restored = await storage.OpenReadAsync(image.ObjectKey, token)
                    ?? throw new InvalidDataException("Restored image object could not be read.");
                await VerifiedCopy.CopyAsync(restored, Stream.Null, image.ByteSize, image.Sha256, token);
            }
            await tools.RestoreAsync(connectionString, dump, token);
            var restoredImages = await BackupDatabase.ImagesAsync(connection, null, token);
            if (!restoredImages.ToHashSet().SetEquals(manifest.Images))
                throw new InvalidDataException("Restored database image associations differ from the manifest.");
        }
        finally
        {
            if (File.Exists(dump)) File.Delete(dump);
            if (File.Exists(objectFile)) File.Delete(objectFile);
        }
    }
}
