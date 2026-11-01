using System.Data.Common;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FirestoreMigration;

public static class MigrationCommand
{
    public static async Task<int> ExecuteAsync(Func<Task<int>> command, TextWriter errors)
    {
        try
        {
            return await command();
        }
        catch (OperationCanceledException)
        {
            errors.WriteLine("CANCELLED: no success claimed. Reconcile any reported Recipe run/journal before cleanup.");
            return 2;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException ||
            exception.InnerException is DbException or DbUpdateException)
        {
            var postgres = exception.GetBaseException() as PostgresException;
            errors.WriteLine($"ERROR: database operation failed ({exception.GetType().Name}, SQLSTATE {postgres?.SqlState ?? "unavailable"}). " +
                "No success claimed. Verify connectivity, schema and conflicts; reconcile any Recipe run/journal before cleanup.");
            return 1;
        }
        catch (AmazonServiceException exception)
        {
            errors.WriteLine($"ERROR: private storage operation failed ({exception.GetType().Name}). " +
                "Reconcile the image journal; no automatic object cleanup or success claimed.");
            return 1;
        }
        catch (Exception exception) when (exception is FormatException or IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or System.Text.Json.JsonException)
        {
            errors.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }
}
