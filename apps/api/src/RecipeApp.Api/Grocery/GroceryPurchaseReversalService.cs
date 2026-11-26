using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryPurchaseReversalService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string CommandType = "purchase-reversal";

    public async Task<GroceryWriteOutcome> ReversePurchaseAsync(
        Guid eventId,
        GroceryCommandMetadata command,
        CancellationToken cancellationToken)
    {
        if (command.CommandId == Guid.Empty)
        {
            return Invalid("Command ID is required.");
        }

        var ownerId = currentUser.UserId;
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var purchaseEvent = await database.GroceryPurchaseEvents
            .SingleOrDefaultAsync(value => value.Id == eventId
                && value.OwnerUserId == ownerId
                && value.Kind == GroceryPurchaseEventKind.Purchase,
                cancellationToken);
        if (purchaseEvent is null)
        {
            return NotFound();
        }

        var list = await database.GroceryLists
            .SingleAsync(value => value.Id == purchaseEvent.GroceryListId
                && value.OwnerUserId == ownerId,
                cancellationToken);
        var fingerprint = GroceryCommandFingerprint.Compute(CommandType, new { eventId, command });
        var replay = await FindReceiptAsync(
            database, list, ownerId, command.CommandId, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var reversal = await FindReversalAsync(
            database, purchaseEvent, ownerId, cancellationToken);
        if (reversal is not null)
        {
            return await SaveNoOpReceiptAsync(reversal.Id);
        }

        if (list.Version != command.ExpectedListVersion)
        {
            return await RecoverAsync(Conflict("The grocery list changed. Reload before undoing this purchase."));
        }

        var item = await database.GroceryItems
            .Include(value => value.Contributions)
            .SingleAsync(value => value.Id == purchaseEvent.GroceryItemId
                && value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id,
                cancellationToken);
        var affectedContributionIds =
            JsonSerializer.Deserialize<Guid[]>(purchaseEvent.AffectedContributionIdsJson) ?? [];
        var targetItem = await database.GroceryItems
            .Include(value => value.Contributions)
            .SingleOrDefaultAsync(value => value.Id != item.Id
                && value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.State == GroceryItemState.Outstanding
                && value.ConsolidationKey == item.ConsolidationKey
                && value.PurchaseUnit == item.PurchaseUnit,
                cancellationToken);
        var restoreItem = targetItem ?? item;
        var affectedContributions = item.Contributions
            .Where(value => affectedContributionIds.Contains(value.Id)
                && value.State == GroceryContributionState.Completed)
            .ToArray();
        foreach (var contribution in affectedContributions)
        {
            contribution.State = GroceryContributionState.Outstanding;
            contribution.GroceryItemId = restoreItem.Id;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        restoreItem.OutstandingPurchaseQuantity += purchaseEvent.Quantity;
        restoreItem.State = GroceryItemState.Outstanding;
        restoreItem.UpdatedAtUtc = now;
        var reversalEvent = new GroceryPurchaseEvent
        {
            GroceryListId = list.Id,
            GroceryItemId = item.Id,
            OwnerUserId = ownerId,
            Kind = GroceryPurchaseEventKind.Reversal,
            Quantity = purchaseEvent.Quantity,
            PurchaseUnit = purchaseEvent.PurchaseUnit,
            OccurredAtUtc = now,
            ItemNameSnapshot = purchaseEvent.ItemNameSnapshot,
            NoteSnapshot = purchaseEvent.NoteSnapshot,
            StoreIdSnapshot = purchaseEvent.StoreIdSnapshot,
            Origin = purchaseEvent.Origin,
            ReversesEventId = purchaseEvent.Id,
            AffectedContributionIdsJson = purchaseEvent.AffectedContributionIdsJson
        };
        database.GroceryPurchaseEvents.Add(reversalEvent);
        GroceryMutationPersistence.TouchList(database, list, now);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(Conflict("The grocery list changed while this purchase was being undone."));
        }
        catch (DbUpdateException exception) when (IsActiveLineConflict(exception))
        {
            return await RecoverAsync(Conflict(
                "Undo would reopen a grocery line that conflicts with another outstanding line. Refresh the list."));
        }

        var receipt = GroceryMutationPersistence.CreateReceipt(
            list, ownerId, command.CommandId, CommandType, fingerprint, [reversalEvent.Id], now);
        database.GroceryCommandReceipts.Add(receipt);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(Conflict("The grocery list changed while this purchase was being undone."));
        }
        catch (DbUpdateException exception) when (
            GroceryMutationPersistence.IsReceiptConflict(exception) || IsActiveLineConflict(exception))
        {
            return await RecoverAsync(Conflict("The grocery list changed while this purchase was being undone."));
        }

        return Success(receipt, StatusCodes.Status201Created);

        async Task<GroceryWriteOutcome> SaveNoOpReceiptAsync(Guid existingReversalId)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var existingReceipt = GroceryMutationPersistence.CreateReceipt(
                list, ownerId, command.CommandId, CommandType, fingerprint, [existingReversalId], now);
            database.GroceryCommandReceipts.Add(existingReceipt);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Success(existingReceipt, StatusCodes.Status200OK);
            }
            catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
            {
                return await RecoverAsync(Conflict("This command ID was already used with different content."));
            }
        }

        async Task<GroceryWriteOutcome> RecoverAsync(GroceryWriteOutcome fallback)
        {
            var committed = await GroceryMutationPersistence.FindReceiptAfterRollbackAsync(
                databaseFactory, transaction, ownerId, command.CommandId, cancellationToken);
            if (committed is not null)
            {
                return committed.CommandType == CommandType && committed.RequestFingerprint == fingerprint
                    ? Success(committed, StatusCodes.Status201Created)
                    : Conflict("This command ID was already used with different content.");
            }

            await using var recoveryDatabase = await databaseFactory.CreateDbContextAsync(cancellationToken);
            var existingReversal = await FindReversalAsync(
                recoveryDatabase, purchaseEvent, ownerId, cancellationToken);
            return existingReversal is null
                ? fallback
                : new GroceryWriteOutcome(
                    new GroceryCommandReceiptResponse(command.CommandId, list.Version, [existingReversal.Id]),
                    StatusCodes.Status200OK);
        }
    }

    private static async Task<GroceryPurchaseEvent?> FindReversalAsync(
        AppDbContext database,
        GroceryPurchaseEvent purchaseEvent,
        Guid ownerId,
        CancellationToken cancellationToken) =>
        await database.GroceryPurchaseEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId
                && value.GroceryListId == purchaseEvent.GroceryListId
                && value.ReversesEventId == purchaseEvent.Id,
                cancellationToken);

    private static async Task<GroceryWriteOutcome?> FindReceiptAsync(
        AppDbContext database,
        GroceryList list,
        Guid ownerId,
        Guid commandId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var receipt = await database.GroceryCommandReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.CommandId == commandId,
                cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        return receipt.CommandType == CommandType && receipt.RequestFingerprint == fingerprint
            ? Success(receipt, StatusCodes.Status201Created)
            : Conflict("This command ID was already used with different content.");
    }

    private static bool IsActiveLineConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_GroceryItems_ActivePurchaseLineKey"
        };

    private static GroceryWriteOutcome Success(GroceryCommandReceipt receipt, int statusCode)
    {
        var response = JsonSerializer.Deserialize<GroceryCommandReceiptResponse>(receipt.ResultJson);
        return response is null
            ? Conflict("The saved grocery command receipt is invalid.")
            : new GroceryWriteOutcome(response, statusCode);
    }

    private static GroceryWriteOutcome NotFound() =>
        new(null, StatusCodes.Status404NotFound);

    private static GroceryWriteOutcome Invalid(string message) =>
        new(null, StatusCodes.Status400BadRequest, "Validation failed", Errors: new Dictionary<string, string[]>
        {
            ["commandId"] = [message]
        });

    private static GroceryWriteOutcome Conflict(string detail) =>
        new(null, StatusCodes.Status409Conflict, "Conflict", detail);
}
