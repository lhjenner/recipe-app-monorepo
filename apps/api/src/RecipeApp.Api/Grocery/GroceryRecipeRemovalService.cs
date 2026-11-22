using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryRecipeRemovalService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string CommandType = "recipe-remove";

    public async Task<GroceryWriteOutcome> RemoveRecipeAsync(
        Guid recipeId,
        GroceryRecipeRemovalCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CommandId == Guid.Empty)
        {
            return GroceryRecipeServiceSupport.Invalid(nameof(command.CommandId), "Command ID is required.");
        }

        var ownerId = currentUser.UserId;
        var fingerprint = GroceryCommandFingerprint.Compute(CommandType, new { recipeId, command });
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var recipeExists = await database.Recipes
            .AnyAsync(value => value.Id == recipeId && value.OwnerUserId == ownerId, cancellationToken);
        if (!recipeExists)
        {
            return GroceryRecipeServiceSupport.NotFound();
        }

        var list = await database.GroceryLists
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId, cancellationToken);
        if (list is null)
        {
            return GroceryRecipeServiceSupport.NotFound();
        }

        var replay = await GroceryRecipeServiceSupport.FindReplayAsync(
            database, list, ownerId, command.CommandId, fingerprint, CommandType, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (list.Version != command.ExpectedListVersion)
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("The grocery list changed. Review the Recipe removal again."));
        }

        var contributions = await database.GroceryContributions
            .Include(value => value.GroceryItem)
                .ThenInclude(value => value!.Contributions)
            .Where(value => value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.State == GroceryContributionState.Outstanding
                && value.RecipeAddition != null
                && value.RecipeAddition.RecipeId == recipeId)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var affectedItems = new HashSet<GroceryItem>();
        foreach (var contribution in contributions)
        {
            contribution.State = GroceryContributionState.Cancelled;
            affectedItems.Add(contribution.GroceryItem!);
        }

        foreach (var item in affectedItems)
        {
            if (!GroceryRecipeQuantityRules.TryRecalculate(item, out var quantity))
            {
                return GroceryRecipeServiceSupport.Invalid(
                    "recipe", "The consolidated purchase target exceeds the supported range.");
            }

            item.OutstandingPurchaseQuantity = quantity;
            item.State = quantity > 0
                ? GroceryItemState.Outstanding
                : GroceryItemState.Cancelled;
            item.UpdatedAtUtc = now;
        }

        GroceryMutationPersistence.TouchList(database, list, now);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("The grocery list changed during this removal. Review it again."));
        }
        catch (DbUpdateException exception) when (GroceryRecipeServiceSupport.IsGroceryLineConflict(exception))
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("A matching grocery line changed during this removal. Review it again."));
        }

        var receipt = GroceryMutationPersistence.CreateReceipt(
            list, ownerId, command.CommandId, CommandType, fingerprint,
            affectedItems.Select(value => value.Id).ToArray(), now);
        database.GroceryCommandReceipts.Add(receipt);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("The grocery list changed during this removal. Review it again."));
        }
        catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("The grocery list changed during this removal. Review it again."));
        }

        return GroceryRecipeServiceSupport.Success(receipt, StatusCodes.Status200OK);

        async Task<GroceryWriteOutcome> RecoverAsync(GroceryWriteOutcome fallback)
        {
            var committed = await GroceryMutationPersistence.FindReceiptAfterRollbackAsync(
                databaseFactory, transaction, ownerId, command.CommandId, cancellationToken);
            if (committed is null)
            {
                return fallback;
            }

            return committed.CommandType == CommandType && committed.RequestFingerprint == fingerprint
                ? GroceryRecipeServiceSupport.Success(committed, StatusCodes.Status200OK)
                : GroceryRecipeServiceSupport.Conflict("This command ID was already used with different content.");
        }
    }
}
