using System.ComponentModel;
using System.Data.Common;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Infrastructure.Storage;

namespace RecipeBackup;

public static class BackupCli
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("export" or "verify" or "restore"))
        {
            Console.Error.WriteLine("Usage: RecipeBackup export|verify|restore <archive-path>");
            return 1;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            var (operation, path) = (args[0], args[1]);
            if (operation == "verify")
            {
                Report(await BackupArchive.VerifyAsync(path, cancellation.Token), path);
                return 0;
            }
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var prefix = operation == "restore" ? "RESTORE_" : "";
            if (operation == "restore" && configuration["RESTORE_CONFIRM_EMPTY"] != "yes")
                throw new InvalidOperationException("Restore requires RESTORE_CONFIRM_EMPTY=yes and isolated empty targets.");
            var url = configuration[prefix + "DATABASE_URL_UNPOOLED"];
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException($"Set {prefix}DATABASE_URL_UNPOOLED; no local database fallback is allowed.");
            var connectionConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["DATABASE_URL"] = url }).Build();
            var connectionString = DatabaseConnection.Get(connectionConfiguration);
            if (new Npgsql.NpgsqlConnectionStringBuilder(connectionString).Host!.Contains("-pooler", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Backup/restore requires a direct, not pooled, database endpoint.");
            var storageConfiguration = new ConfigurationBuilder().AddInMemoryCollection(
                new[] { "AWS_ENDPOINT_URL_S3", "AWS_REGION", "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY" }
                    .ToDictionary(key => key, key => configuration[prefix + key])).Build();
            var bucket = configuration[prefix + "BACKUP_BUCKET"];
            if (string.IsNullOrWhiteSpace(bucket)) throw new InvalidOperationException($"Set {prefix}BACKUP_BUCKET explicitly.");
            using var client = ObjectStorageRegistration.CreateClient(storageConfiguration);
            var storage = new S3PrivateObjectStorage(client, Options.Create(new ObjectStorageOptions { BucketName = bucket }));
            var runner = new BackupRunner(new PostgresTools());
            if (operation == "export")
                Report(await runner.ExportAsync(connectionString, storage, path, cancellation.Token), path);
            else
            {
                Console.WriteLine("Restore started: any failure may leave partial objects; use fresh isolated targets for a retry.");
                await runner.RestoreAsync(connectionString, client, bucket, storage, path, cancellation.Token);
                Console.WriteLine("PASS: database restored and all referenced objects/associations verified.");
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("CANCELLED: no success claimed; inspect isolated restore targets for partial data.");
            return 2;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
            or InvalidOperationException or DbException
            or AmazonServiceException or Win32Exception or HttpRequestException)
        {
            Console.Error.WriteLine($"FAILED ({exception.GetType().Name}). No success claimed; no provider diagnostics or credentials printed.");
            if (exception is InvalidOperationException or InvalidDataException) Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    private static void Report(BackupManifest manifest, string path)
    {
        var bytes = new FileInfo(path).Length;
        Console.WriteLine($"PASS: archive verified; associations={manifest.Images.Count}; "
            + $"databaseBytes={manifest.DatabaseBytes}; uniqueImageBytes={manifest.Images.DistinctBy(row => row.Sha256).Sum(row => row.ByteSize)}; "
            + $"archiveBytes={bytes}; sixCopiesBytes={bytes * 6}; sevenCopiesBytes={bytes * 7}.");
        Console.WriteLine("WARNING: this is a plaintext recovery archive. Do not commit or upload it; encryption is not configured.");
    }
}
