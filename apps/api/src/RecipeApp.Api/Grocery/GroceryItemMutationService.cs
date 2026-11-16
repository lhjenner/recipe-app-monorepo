using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryItemMutationService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string CommandType = "item-update";

    public async Task<GroceryWriteOutcome> UpdateItemAsync(
        Guid itemId,
        GroceryItemUpdateRequest request,
        CancellationToken cancellationToken)
    {
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
        var replay = await FindReplayAsync(
            database, list, ownerId, request.CommandId, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (list.Version != request.ExpectedListVersion)
        {
            return await RecoverAsync(Conflict("The grocery list changed. Reload it and review the edit again."));
        }

        if (item.State != GroceryItemState.Outstanding)
        {
            return await RecoverAsync(Conflict("Only outstanding grocery items can be edited."));
        }

        var errors = GroceryWriteValidator.ValidateUpdate(request, item.PurchaseUnit);
        if (errors.Count != 0)
        {
            return Invalid(errors);
        }

        var recipeFloor = GroceryItemUpdateRules.GetRecipePurchaseFloor(item);
        if (request.OutstandingPurchaseQuantity < recipeFloor)
        {
            return Invalid(new Dictionary<string, string[]>
            {
                [nameof(request.OutstandingPurchaseQuantity)] =
                [
                    $"Purchase quantity cannot be lower than the {recipeFloor} required by outstanding Recipes. Remove the grocery line to clear Recipe demand."
                ]
            });
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (item.IngredientId is not null
            && (request.AdHocStoreId is not null || request.AdHocDepartmentId is not null))
        {
            return Invalid(new Dictionary<string, string[]>
            {
                ["adHocDepartmentId"] = ["Linked Ingredients use the selected store's master department."]
            });
        }

        if (request.DepartmentUpdate is not null)
        {
            var departmentError = await GroceryItemUpdateRules.ApplyDepartmentUpdateAsync(
                database, item, request.DepartmentUpdate, ownerId, now, cancellationToken);
            if (departmentError is not null)
            {
                return departmentError.StatusCode == StatusCodes.Status409Conflict
                    ? await RecoverAsync(departmentError)
                    : departmentError;
            }
        }
        else if (item.IngredientId is null)
        {
            var storeIsValid = request.AdHocStoreId is null
                || await database.Stores.AnyAsync(value => value.Id == request.AdHocStoreId, cancellationToken);
            var departmentIsValid = request.AdHocDepartmentId is null
                || await database.Departments.AnyAsync(value =>
                    value.Id == request.AdHocDepartmentId && value.OwnerUserId == ownerId,
                    cancellationToken);
            if (!storeIsValid || !departmentIsValid)
            {
                return Invalid(new Dictionary<string, string[]>
                {
                    ["adHocDepartmentId"] = ["The selected store or department does not exist."]
                });
            }

            item.StoreId = request.AdHocStoreId;
            item.DepartmentId = request.AdHocDepartmentId;
        }
        GroceryItemUpdateRules.AdjustManualDemand(
            item, request.OutstandingPurchaseQuantity - recipeFloor, now);
        item.OutstandingPurchaseQuantity = request.OutstandingPurchaseQuantity;
        item.Note = CleanOptional(request.Note);
        item.UpdatedAtUtc = now;
        GroceryMutationPersistence.TouchList(database, list, now);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(Conflict(
                "The grocery list or Ingredient changed during this edit. Reload and review it again."));
        }

        var receipt = GroceryMutationPersistence.CreateReceipt(
            list, ownerId, request.CommandId, CommandType, fingerprint, [item.Id], now);
        database.GroceryCommandReceipts.Add(receipt);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(Conflict(
                "The grocery list or Ingredient changed during this edit. Reload and review it again."));
        }
        catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
        {
            return await RecoverAsync(Conflict(
                "The grocery list or Ingredient changed during this edit. Reload and review it again."));
        }

        return Success(receipt);

        async Task<GroceryWriteOutcome> RecoverAsync(GroceryWriteOutcome fallback)
        {
            var committed = await GroceryMutationPersistence.FindReceiptAfterRollbackAsync(
                databaseFactory, transaction, ownerId, request.CommandId, cancellationToken);
            if (committed is null)
            {
                return fallback;
            }
            return committed.CommandType == CommandType && committed.RequestFingerprint == fingerprint
                ? Success(committed)
                : Conflict("This command ID was already used with different content.");
        }
    }

    private static async Task<GroceryWriteOutcome?> FindReplayAsync(
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
            ? Success(receipt)
            : Conflict("This command ID was already used with different content.");
    }

    private static GroceryWriteOutcome Success(GroceryCommandReceipt receipt)
    {
        var response = JsonSerializer.Deserialize<GroceryCommandReceiptResponse>(receipt.ResultJson);
        return response is null
            ? Conflict("The saved grocery command receipt is invalid.")
            : new GroceryWriteOutcome(response, StatusCodes.Status200OK);
    }

    private static GroceryWriteOutcome NotFound() =>
        new(null, StatusCodes.Status404NotFound);

    private static GroceryWriteOutcome Invalid(Dictionary<string, string[]> errors) =>
        new(null, StatusCodes.Status400BadRequest, "Validation failed", Errors: errors);

    private static GroceryWriteOutcome Conflict(string detail) =>
        new(null, StatusCodes.Status409Conflict, "Conflict", detail);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
