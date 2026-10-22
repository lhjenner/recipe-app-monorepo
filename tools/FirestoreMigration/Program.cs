using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

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
            var options = MigrationOptions.Parse(args);
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<MigrationOptions>(optional: true)
                .AddEnvironmentVariables()
                .Build();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection with tool User Secrets or environment variables.");
            await using var database = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString).Options);
            var result = await new MigrationRunner(database, Console.In, Console.Out).RunAsync(options, cancellation.Token);
            return result.Errors == 0 ? 0 : 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("CANCELLED: no migration transaction was committed.");
            return 2;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException ||
            exception.InnerException is DbException or DbUpdateException)
        {
            var postgres = exception.GetBaseException() as PostgresException;
            Console.Error.WriteLine($"ERROR: database operation failed ({exception.GetType().Name}, SQLSTATE {postgres?.SqlState ?? "unavailable"}). " +
                "The transaction was not committed. Verify connectivity, schema and conflicts, then review again.");
            return 1;
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }
}
