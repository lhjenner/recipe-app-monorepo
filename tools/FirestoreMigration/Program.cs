using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.Infrastructure.Storage;

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
        return await MigrationCommand.ExecuteAsync(async () =>
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
        }, Console.Error);
    }
}
