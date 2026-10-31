using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Infrastructure.Storage;
using Amazon.Runtime;

namespace FirestoreMigration;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--help")
        {
            Console.WriteLine(MigrationOptions.Usage);
            return 0;
        }
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        try
        {
            var recovery = args.Length > 0 && args[0] == "--recover-image-journal";
            var options = recovery ? null : MigrationOptions.Parse(args);
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<MigrationOptions>(optional: true)
                .AddEnvironmentVariables()
                .Build();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection with tool User Secrets or environment variables.");
            await using var database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString).Options);
            if (recovery)
                return await RecipeImageCli.RecoverAsync(args, database, configuration, Console.Out, cancellation.Token);
            if (options!.Mode == MigrationMode.Recipes)
            {
                using var storageClient = options.Write && options.ImageManifestFile is not null
                    ? ObjectStorageRegistration.CreateClient(configuration) : null;
                var storage = storageClient is null ? null : RecipeImageCli.Storage(configuration, storageClient);
                var recipeResult = await new RecipeMigrationRunner(database, Console.Out, Console.In, storage)
                    .RunAsync(options, cancellation.Token);
                return recipeResult.Errors == 0 ? 0 : 1;
            }
            var result = await new MigrationRunner(database, Console.In, Console.Out).RunAsync(options, cancellation.Token);
            return result.Errors == 0 ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("CANCELLED: no success claimed. Reconcile any reported Recipe run/journal before cleanup.");
            return 2;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException ||
            exception.InnerException is DbException or DbUpdateException)
        {
            var postgres = exception.GetBaseException() as PostgresException;
            Console.Error.WriteLine($"ERROR: database operation failed ({exception.GetType().Name}, SQLSTATE {postgres?.SqlState ?? "unavailable"}). " +
                "No success claimed. Verify connectivity, schema and conflicts; reconcile any Recipe run/journal before cleanup.");
            return 1;
        }
        catch (AmazonServiceException exception)
        {
            Console.Error.WriteLine($"ERROR: private storage operation failed ({exception.GetType().Name}). " +
                "Reconcile the image journal; no automatic object cleanup or success claimed.");
            return 1;
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }
}
