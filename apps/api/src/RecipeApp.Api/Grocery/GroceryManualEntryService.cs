using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Ingredients;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryManualEntryService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string CommandType = "manual-add";

    public async Task<GroceryWriteOutcome> AddManualItemsAsync(
        GroceryManualEntryCommand command,
        CancellationToken cancellationToken)
    {
        var errors = GroceryWriteValidator.ValidateAdd(command);
        if (errors.Count != 0)
        {
            return Invalid(errors);
        }

        var ownerId = currentUser.UserId;
        var fingerprint = GroceryCommandFingerprint.Compute(CommandType, command);
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var list = await database.GroceryLists
            .SingleOrDefaultAsync(item => item.OwnerUserId == ownerId, cancellationToken);
        if (list is null)
        {
            if (command.ExpectedListVersion != 0)
            {
                return Conflict("The grocery list changed. Reload it and review the addition again.");
            }

            list = new GroceryList
            {
                OwnerUserId = ownerId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            database.GroceryLists.Add(list);
            try
            {
                await database.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsOwnerListConflict(exception))
            {
                return Conflict("The grocery list was created elsewhere. Reload it and review the addition again.");
            }
        }
        else
        {
            var replay = await FindReplayAsync(
                database, list, ownerId, command.CommandId, fingerprint, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }

            if (list.Version != command.ExpectedListVersion)
            {
                return Conflict("The grocery list changed. Reload it and review the addition again.");
            }
        }

        var prepared = await GroceryManualEntryPreparation.PrepareAsync(
            database, ownerId, command, now, cancellationToken);
        if (prepared.IsNotFound)
        {
            return new GroceryWriteOutcome(null, StatusCodes.Status404NotFound);
        }

        if (prepared.ConflictDetail is not null)
        {
            return new GroceryWriteOutcome(
                null,
                StatusCodes.Status409Conflict,
                "Ingredient name conflict",
                prepared.ConflictDetail);
        }

        if (prepared.Errors.Count != 0)
        {
            return Invalid(prepared.Errors);
        }

        var activeItems = await database.GroceryItems
            .Where(item => item.OwnerUserId == ownerId
                && item.GroceryListId == list.Id
                && item.State == GroceryItemState.Outstanding)
            .ToListAsync(cancellationToken);
        var duplicateReviews = GroceryManualEntryProcessor.BuildDuplicateReviews(
            prepared.Entries, activeItems);
        var submittedReviews = command.ReviewedDuplicates ?? [];
        if (!duplicateReviews.SequenceEqual(submittedReviews.OrderBy(review => review.EntryIndex)))
        {
            return new GroceryWriteOutcome(
                null,
                StatusCodes.Status409Conflict,
                "Review duplicate grocery items",
                "One or more entries match an outstanding item. Review the current quantity and confirm the increment.",
                DuplicateReviews: duplicateReviews);
        }

        foreach (var ingredient in prepared.NewIngredients)
        {
            database.Ingredients.Add(ingredient);
            var source = command.Items.First(item => item.SaveAsIngredient
                && IngredientWriteValidator.Normalize(item.Name ?? string.Empty) == ingredient.NormalizedName);
            IngredientWritePersistence.AddMappings(database, ingredient, source.StoreMappings ?? []);
        }

        var resultIds = GroceryManualEntryProcessor.ApplyEntries(
            database, list, prepared.Entries, activeItems, duplicateReviews, now);
        GroceryMutationPersistence.TouchList(database, list, now);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The grocery list changed during this save. Reload and review the addition again.");
        }
        catch (DbUpdateException exception) when (IsGroceryLineConflict(exception))
        {
            return Conflict("A matching grocery line changed during this save. Reload and review the addition again.");
        }
        catch (DbUpdateException exception) when (IsIngredientNameConflict(exception))
        {
            return Conflict(
                "An Ingredient with this name was added elsewhere. Select it from the Ingredient lookup or restore it before retrying.");
        }

        var receipt = GroceryMutationPersistence.CreateReceipt(
            list, ownerId, command.CommandId, CommandType, fingerprint, resultIds, now);
        database.GroceryCommandReceipts.Add(receipt);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The grocery list changed during this save. Reload and review the addition again.");
        }
        catch (DbUpdateException exception) when (IsReceiptConflict(exception))
        {
            return await FindReplayAfterConflictAsync(
                ownerId, list.Id, command.CommandId, fingerprint, cancellationToken);
        }

        return Success(receipt);
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
            .SingleOrDefaultAsync(item => item.OwnerUserId == ownerId
                && item.GroceryListId == list.Id
                && item.CommandId == commandId, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        return receipt.CommandType == CommandType && receipt.RequestFingerprint == fingerprint
            ? Success(receipt)
            : Conflict("This command ID was already used with different content.");
    }

    private async Task<GroceryWriteOutcome> FindReplayAfterConflictAsync(
        Guid ownerId,
        Guid listId,
        Guid commandId,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var receipt = await database.GroceryCommandReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OwnerUserId == ownerId
                && item.GroceryListId == listId
                && item.CommandId == commandId, cancellationToken);
        return receipt is not null
            && receipt.CommandType == CommandType
            && receipt.RequestFingerprint == fingerprint
                ? Success(receipt)
                : Conflict("The grocery list changed during this save. Reload and review the addition again.");
    }

    private static GroceryWriteOutcome Success(GroceryCommandReceipt receipt)
    {
        var response = JsonSerializer.Deserialize<GroceryCommandReceiptResponse>(receipt.ResultJson);
        return response is null
            ? Conflict("The saved grocery command receipt is invalid.")
            : new GroceryWriteOutcome(response, StatusCodes.Status201Created);
    }

    private static GroceryWriteOutcome Invalid(Dictionary<string, string[]> errors) =>
        new(null, StatusCodes.Status400BadRequest, "Validation failed", Errors: errors);

    private static GroceryWriteOutcome Conflict(string detail) =>
        new(null, StatusCodes.Status409Conflict, "Conflict", detail);

    private static bool IsOwnerListConflict(DbUpdateException exception) =>
        IsUniqueViolation(exception, "IX_GroceryLists_OwnerUserId");

    private static bool IsGroceryLineConflict(DbUpdateException exception) =>
        IsUniqueViolation(exception, "IX_GroceryItems_ActivePurchaseLineKey");

    private static bool IsIngredientNameConflict(DbUpdateException exception) =>
        IsUniqueViolation(exception, "IX_Ingredients_OwnerUserId_NormalizedName");

    private static bool IsReceiptConflict(DbUpdateException exception) =>
        IsUniqueViolation(exception, "IX_GroceryCommandReceipts_Owner_List_Command");

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgres && postgres.ConstraintName == constraintName;
}
