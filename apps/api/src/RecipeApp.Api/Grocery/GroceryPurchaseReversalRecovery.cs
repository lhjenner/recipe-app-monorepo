using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryPurchaseReversalRecovery
{
    public static async Task<GroceryCommandReceipt?> SaveNoOpAsync(
        IDbContextFactory<AppDbContext> databaseFactory,
        Guid ownerId,
        Guid listId,
        Guid purchaseEventId,
        Guid commandId,
        string commandType,
        string fingerprint,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var reversalId = await database.GroceryPurchaseEvents.AsNoTracking()
            .Where(value => value.OwnerUserId == ownerId && value.GroceryListId == listId
                && value.ReversesEventId == purchaseEventId)
            .Select(value => (Guid?)value.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (reversalId is null) return null;

        var list = await database.GroceryLists.AsNoTracking()
            .SingleAsync(value => value.Id == listId && value.OwnerUserId == ownerId, cancellationToken);
        var receipt = GroceryMutationPersistence.CreateReceipt(
            list, ownerId, commandId, commandType, fingerprint, [reversalId.Value], now);
        database.GroceryCommandReceipts.Add(receipt);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            return receipt;
        }
        catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
        {
            await using var retryDatabase = await databaseFactory.CreateDbContextAsync(cancellationToken);
            return await retryDatabase.GroceryCommandReceipts.AsNoTracking()
                .SingleAsync(value => value.OwnerUserId == ownerId && value.GroceryListId == listId
                    && value.CommandId == commandId, cancellationToken);
        }
    }
}
