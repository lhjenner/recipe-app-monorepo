using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryPurchaseService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string CommandType = "item-purchase";

    public async Task<GroceryWriteOutcome> PurchaseItemAsync(
        Guid itemId,
        GroceryPurchaseRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CommandId == Guid.Empty)
        {
            return Invalid(nameof(request.CommandId), "Command ID is required.");
        }

        var ownerId = currentUser.UserId;
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var item = await database.GroceryItems
            .Include(value => value.Contributions)
            .SingleOrDefaultAsync(value => value.Id == itemId && value.OwnerUserId == ownerId, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        var list = await database.GroceryLists
            .SingleAsync(value => value.Id == item.GroceryListId && value.OwnerUserId == ownerId, cancellationToken);
        var fingerprint = GroceryCommandFingerprint.Compute(CommandType, new { itemId, request });
        var replay = await FindReceiptAsync(
            database, list, ownerId, request.CommandId, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (list.Version != request.ExpectedListVersion)
        {
            return await RecoverAsync(Conflict("The grocery list changed. Reload before purchasing this item."));
        }

        if (item.State != GroceryItemState.Outstanding)
        {
            return await RecoverAsync(Conflict("Only outstanding grocery items can be purchased."));
        }

        if (request.ExpectedOutstandingQuantity <= 0
            || request.ExpectedOutstandingQuantity != item.OutstandingPurchaseQuantity
            || !string.Equals(request.PurchaseUnit, item.PurchaseUnit, StringComparison.Ordinal))
        {
            return Invalid(
                nameof(request.ExpectedOutstandingQuantity),
                "Purchase requests must match the entire current outstanding quantity and unit.");
        }

        if (request.StoreId is not null
            && !await database.Stores.AnyAsync(value => value.Id == request.StoreId, cancellationToken))
        {
            return Invalid(nameof(request.StoreId), "The selected store does not exist.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var affectedContributionIds = item.Contributions
            .Where(value => value.State == GroceryContributionState.Outstanding)
            .Select(value => value.Id)
            .ToArray();
        foreach (var contribution in item.Contributions.Where(
                     value => affectedContributionIds.Contains(value.Id)))
        {
            contribution.State = GroceryContributionState.Completed;
        }

        item.State = GroceryItemState.Completed;
        item.OutstandingPurchaseQuantity = 0;
        item.UpdatedAtUtc = now;
        var purchaseEvent = new GroceryPurchaseEvent
        {
            GroceryListId = list.Id,
            GroceryItemId = item.Id,
            OwnerUserId = ownerId,
            Kind = GroceryPurchaseEventKind.Purchase,
            Quantity = request.ExpectedOutstandingQuantity,
            PurchaseUnit = item.PurchaseUnit,
            OccurredAtUtc = now,
            ItemNameSnapshot = item.NameSnapshot,
            NoteSnapshot = item.Note,
            StoreIdSnapshot = request.StoreId,
            Origin = GroceryPurchaseOrigin.Manual,
            AffectedContributionIdsJson = JsonSerializer.Serialize(affectedContributionIds)
        };
        database.GroceryPurchaseEvents.Add(purchaseEvent);
        GroceryMutationPersistence.TouchList(database, list, now);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(Conflict("The grocery list changed while this purchase was being saved."));
        }

        var receipt = GroceryMutationPersistence.CreateReceipt(
            list, ownerId, request.CommandId, CommandType, fingerprint, [purchaseEvent.Id], now);
        database.GroceryCommandReceipts.Add(receipt);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(Conflict("The grocery list changed while this purchase was being saved."));
        }
        catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
        {
            return await RecoverAsync(Conflict("The grocery list changed while this purchase was being saved."));
        }

        return Success(receipt, StatusCodes.Status201Created);

        async Task<GroceryWriteOutcome> RecoverAsync(GroceryWriteOutcome fallback)
        {
            var committed = await GroceryMutationPersistence.FindReceiptAfterRollbackAsync(
                databaseFactory, transaction, ownerId, request.CommandId, cancellationToken);
            if (committed is null)
            {
                return fallback;
            }

            return committed.CommandType == CommandType && committed.RequestFingerprint == fingerprint
                ? Success(committed, StatusCodes.Status201Created)
                : Conflict("This command ID was already used with different content.");
        }
    }

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
                && value.CommandId == commandId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        return receipt.CommandType == CommandType && receipt.RequestFingerprint == fingerprint
            ? Success(receipt, StatusCodes.Status201Created)
            : Conflict("This command ID was already used with different content.");
    }

    private static GroceryWriteOutcome Success(GroceryCommandReceipt receipt, int statusCode)
    {
        var response = JsonSerializer.Deserialize<GroceryCommandReceiptResponse>(receipt.ResultJson);
        return response is null
            ? Conflict("The saved grocery command receipt is invalid.")
            : new GroceryWriteOutcome(response, statusCode);
    }

    private static GroceryWriteOutcome NotFound() =>
        new(null, StatusCodes.Status404NotFound);

    private static GroceryWriteOutcome Invalid(string field, string message) =>
        new(null, StatusCodes.Status400BadRequest, "Validation failed", Errors: new Dictionary<string, string[]>
        {
            [field] = [message]
        });

    private static GroceryWriteOutcome Conflict(string detail) =>
        new(null, StatusCodes.Status409Conflict, "Conflict", detail);
}
