using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryMutationPersistence
{
    public static async Task<GroceryCommandReceipt?> FindReceiptAfterRollbackAsync(
        IDbContextFactory<AppDbContext> databaseFactory,
        IDbContextTransaction transaction,
        Guid ownerId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        return await database.GroceryCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId && value.CommandId == commandId,
                cancellationToken);
    }

    public static bool IsReceiptConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_GroceryCommandReceipts_Owner_List_Command"
        };

    public static void TouchList(AppDbContext database, GroceryList list, DateTime nowUtc)
    {
        list.UpdatedAtUtc = nowUtc;
        database.Entry(list).Property(item => item.UpdatedAtUtc).IsModified = true;
        database.Entry(list).Property(item => item.Version).OriginalValue = list.Version;
    }

    public static GroceryCommandReceipt CreateReceipt(
        GroceryList list,
        Guid ownerId,
        Guid commandId,
        string commandType,
        string fingerprint,
        IReadOnlyList<Guid> resultIds,
        DateTime nowUtc)
    {
        var response = new GroceryCommandReceiptResponse(commandId, list.Version, resultIds);
        return new GroceryCommandReceipt
        {
            GroceryListId = list.Id,
            OwnerUserId = ownerId,
            CommandId = commandId,
            CommandType = commandType,
            RequestFingerprint = fingerprint,
            ResultJson = JsonSerializer.Serialize(response),
            ResultListVersion = list.Version,
            CommittedAtUtc = nowUtc
        };
    }
}
