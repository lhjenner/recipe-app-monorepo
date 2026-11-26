using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryItemCancellationService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string CommandType = "item-cancel";

    public async Task<GroceryCancelOutcome> CancelItemAsync(
        Guid itemId,
        GroceryCommandMetadata command,
        CancellationToken cancellationToken)
    {
        if (command.CommandId == Guid.Empty)
        {
            return new GroceryCancelOutcome(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "Command ID is required.");
        }

        var ownerId = currentUser.UserId;
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var item = await database.GroceryItems
            .Include(value => value.Contributions)
            .SingleOrDefaultAsync(value => value.Id == itemId && value.OwnerUserId == ownerId, cancellationToken);
        if (item is null)
        {
            return new GroceryCancelOutcome(StatusCodes.Status404NotFound);
        }

        var list = await database.GroceryLists
            .SingleAsync(value => value.Id == item.GroceryListId && value.OwnerUserId == ownerId, cancellationToken);
        var fingerprint = GroceryCommandFingerprint.Compute(CommandType, new { itemId, command });
        var receipt = await database.GroceryCommandReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.CommandId == command.CommandId, cancellationToken);
        if (receipt is not null)
        {
            return receipt.CommandType == CommandType && receipt.RequestFingerprint == fingerprint
                ? new GroceryCancelOutcome(StatusCodes.Status204NoContent)
                : Conflict("This command ID was already used with different content.");
        }

        if (list.Version != command.ExpectedListVersion)
        {
            return await RecoverAsync("The grocery list changed. Reload it before removing this item.");
        }

        if (item.State != GroceryItemState.Outstanding)
        {
            return await RecoverAsync("Only outstanding grocery items can be removed.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        item.State = GroceryItemState.Cancelled;
        item.OutstandingPurchaseQuantity = 0;
        item.UpdatedAtUtc = now;
        foreach (var contribution in item.Contributions.Where(value =>
                     value.State == GroceryContributionState.Outstanding))
        {
            contribution.State = GroceryContributionState.Cancelled;
        }

        GroceryMutationPersistence.TouchList(database, list, now);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync("The grocery list changed. Reload it before removing this item.");
        }

        database.GroceryCommandReceipts.Add(GroceryMutationPersistence.CreateReceipt(
            list, ownerId, command.CommandId, CommandType, fingerprint, [item.Id], now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync("The grocery list changed. Reload it before removing this item.");
        }
        catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
        {
            return await RecoverAsync("The grocery list changed. Reload it before removing this item.");
        }

        return new GroceryCancelOutcome(StatusCodes.Status204NoContent);

        async Task<GroceryCancelOutcome> RecoverAsync(string fallback)
        {
            var committed = await GroceryMutationPersistence.FindReceiptAfterRollbackAsync(
                databaseFactory, transaction, ownerId, command.CommandId, cancellationToken);
            if (committed is null)
            {
                return Conflict(fallback);
            }
            return committed.CommandType == CommandType && committed.RequestFingerprint == fingerprint
                ? new GroceryCancelOutcome(StatusCodes.Status204NoContent)
                : Conflict("This command ID was already used with different content.");
        }
    }

    public async Task<GroceryCancelOutcome> CancelAllItemsAsync(
        GroceryCommandMetadata command,
        CancellationToken cancellationToken)
    {
        if (command.CommandId == Guid.Empty)
        {
            return new GroceryCancelOutcome(
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "Command ID is required.");
        }

        var ownerId = currentUser.UserId;
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var list = await database.GroceryLists
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId, cancellationToken);
        if (list is null)
        {
            return new GroceryCancelOutcome(StatusCodes.Status204NoContent);
        }

        const string commandType = "items-cancel";
        var fingerprint = GroceryCommandFingerprint.Compute(commandType, command);
        var existingReceipt = await database.GroceryCommandReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.CommandId == command.CommandId,
                cancellationToken);
        if (existingReceipt is not null)
        {
            return existingReceipt.CommandType == commandType
                && existingReceipt.RequestFingerprint == fingerprint
                ? new GroceryCancelOutcome(StatusCodes.Status204NoContent)
                : Conflict("This command ID was already used with different content.");
        }

        if (list.Version != command.ExpectedListVersion)
        {
            return await RecoverAsync("The grocery list changed. Reload it before clearing outstanding items.");
        }

        var items = await database.GroceryItems
            .Include(value => value.Contributions)
            .Where(value => value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.State == GroceryItemState.Outstanding)
            .ToArrayAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in items)
        {
            item.State = GroceryItemState.Cancelled;
            item.OutstandingPurchaseQuantity = 0;
            item.UpdatedAtUtc = now;
            foreach (var contribution in item.Contributions.Where(
                         value => value.State == GroceryContributionState.Outstanding))
            {
                contribution.State = GroceryContributionState.Cancelled;
            }
        }

        if (items.Length > 0)
        {
            GroceryMutationPersistence.TouchList(database, list, now);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return await RecoverAsync("The grocery list changed. Reload it before clearing outstanding items.");
            }
        }

        database.GroceryCommandReceipts.Add(GroceryMutationPersistence.CreateReceipt(
            list, ownerId, command.CommandId, commandType, fingerprint, items.Select(item => item.Id).ToArray(), now));
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync("The grocery list changed. Reload it before clearing outstanding items.");
        }
        catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
        {
            return await RecoverAsync("The grocery list changed. Reload it before clearing outstanding items.");
        }

        return new GroceryCancelOutcome(StatusCodes.Status204NoContent);

        async Task<GroceryCancelOutcome> RecoverAsync(string fallback)
        {
            var committed = await GroceryMutationPersistence.FindReceiptAfterRollbackAsync(
                databaseFactory, transaction, ownerId, command.CommandId, cancellationToken);
            if (committed is null)
            {
                return Conflict(fallback);
            }

            return committed.CommandType == commandType && committed.RequestFingerprint == fingerprint
                ? new GroceryCancelOutcome(StatusCodes.Status204NoContent)
                : Conflict("This command ID was already used with different content.");
        }
    }

    private static GroceryCancelOutcome Conflict(string detail) =>
        new(StatusCodes.Status409Conflict, "Conflict", detail);
}
