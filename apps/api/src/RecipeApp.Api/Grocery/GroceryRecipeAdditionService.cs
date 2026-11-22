using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

public sealed class GroceryRecipeAdditionService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
{
    private const string CommandType = "recipe-add";

    public async Task<GroceryWriteOutcome> AddRecipeAsync(
        Guid recipeId,
        GroceryRecipeAddCommand command,
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
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var list = await database.GroceryLists
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId, cancellationToken);
        if (list is not null)
        {
            var replay = await GroceryRecipeServiceSupport.FindReplayAsync(
                database, list, ownerId, command.CommandId, fingerprint, CommandType, cancellationToken);
            if (replay is not null)
            {
                return replay;
            }
        }
        else if (command.ExpectedListVersion != 0)
        {
            return GroceryRecipeServiceSupport.Conflict("The grocery list changed. Review the Recipe addition again.");
        }

        await GroceryRecipeServiceSupport.LockRecipeAsync(database, recipeId, ownerId, cancellationToken);
        await GroceryRecipeServiceSupport.LockRecipeIngredientsAsync(database, recipeId, ownerId, cancellationToken);
        var recipe = await GroceryRecipeServiceSupport.LoadRecipeAsync(
            database, recipeId, ownerId, tracked: true, cancellationToken);
        if (recipe is null)
        {
            var belongsToOwner = await database.Recipes
                .AnyAsync(value => value.Id == recipeId && value.OwnerUserId == ownerId, cancellationToken);
            return belongsToOwner
                ? GroceryRecipeServiceSupport.Conflict("The Recipe is no longer active. Review the current Recipe before adding it.")
                : GroceryRecipeServiceSupport.NotFound();
        }

        if (GroceryRecipeServiceSupport.HasMissingIngredientReference(recipe))
        {
            return GroceryRecipeServiceSupport.NotFound();
        }

        if (recipe.Version != command.ExpectedRecipeVersion)
        {
            return GroceryRecipeServiceSupport.Conflict("The Recipe changed after review. Preview it again before adding it.");
        }

        if (GroceryRecipeServiceSupport.GetIngredientFingerprint(recipe) != command.ExpectedIngredientFingerprint)
        {
            return await RecoverAsync(GroceryRecipeServiceSupport.Conflict("An Ingredient changed after review. Preview the Recipe again."));
        }

        if (list is not null && list.Version != command.ExpectedListVersion)
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("The grocery list changed after review. Preview the addition again."));
        }

        var eligible = GroceryRecipeServiceSupport.GetEligibleItems(recipe);
        if (eligible.Count == 0)
        {
            return GroceryRecipeServiceSupport.Invalid("recipe", "This Recipe has no eligible Ingredients to add.");
        }

        var hasOutstandingAddition = list is not null
            && await GroceryRecipeServiceSupport.HasOutstandingAdditionAsync(
                database, recipeId, ownerId, list.Id, cancellationToken);
        if (hasOutstandingAddition && !command.ConfirmRepeat)
        {
            return GroceryRecipeServiceSupport.Conflict(
                "This Recipe still has outstanding requirements. Confirm another Recipe's worth or cancel.");
        }

        if (list is null)
        {
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
            catch (DbUpdateException exception) when (GroceryRecipeServiceSupport.IsOwnerListConflict(exception))
            {
                return await RecoverAsync(
                    GroceryRecipeServiceSupport.Conflict("The grocery list was created elsewhere. Review the addition again."));
            }
        }

        var addition = new GroceryRecipeAddition
        {
            GroceryListId = list.Id,
            OwnerUserId = ownerId,
            RecipeId = recipe.Id,
            RecipeNameSnapshot = recipe.Name,
            RecipeVersion = recipe.Version,
            AddedAtUtc = now
        };
        database.GroceryRecipeAdditions.Add(addition);

        var activeItems = await database.GroceryItems
            .Include(value => value.Contributions)
            .Where(value => value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.State == GroceryItemState.Outstanding)
            .ToListAsync(cancellationToken);
        var itemsByTarget = activeItems.ToDictionary(
            value => GroceryRecipeServiceSupport.TargetKey(value.ConsolidationKey, value.PurchaseUnit),
            StringComparer.Ordinal);
        var affectedItems = new HashSet<GroceryItem>();
        foreach (var entry in eligible)
        {
            var key = GroceryRecipeServiceSupport.GetConsolidationKey(entry);
            var purchaseUnit = GroceryRecipeServiceSupport.GetPurchaseUnit(entry.Interpretation);
            var targetKey = GroceryRecipeServiceSupport.TargetKey(key, purchaseUnit);
            if (!itemsByTarget.TryGetValue(targetKey, out var groceryItem))
            {
                groceryItem = new GroceryItem
                {
                    GroceryListId = list.Id,
                    OwnerUserId = ownerId,
                    IngredientId = entry.Item.IngredientId,
                    NameSnapshot = entry.Item.Ingredient!.Name,
                    NormalizedName = entry.Item.Ingredient.NormalizedName,
                    Dimension = entry.Interpretation.Dimension,
                    CountLabel = entry.Interpretation.CountLabel,
                    ConsolidationKey = key,
                    PurchaseUnit = purchaseUnit,
                    State = GroceryItemState.Outstanding,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                database.GroceryItems.Add(groceryItem);
                itemsByTarget.Add(targetKey, groceryItem);
            }

            groceryItem.Contributions.Add(new GroceryContribution
            {
                GroceryListId = list.Id,
                GroceryItemId = groceryItem.Id,
                OwnerUserId = ownerId,
                Origin = GroceryContributionOrigin.Recipe,
                RecipeAdditionId = addition.Id,
                OriginalNameText = entry.Item.Text,
                OriginalAmountText = entry.Item.QuantityText,
                OriginalUnitText = entry.Item.UnitText,
                OriginalNotesText = entry.Item.NotesText,
                InterpretedAmount = entry.Interpretation.Amount,
                Dimension = entry.Interpretation.Dimension,
                CountLabel = entry.Interpretation.CountLabel,
                UnresolvedReason = entry.Interpretation.UnresolvedReason,
                SourceRecipeId = recipe.Id,
                SourceRecipeStepId = entry.Step.Id,
                SourceRecipeStepItemId = entry.Item.Id,
                SourceRecipeNameSnapshot = recipe.Name,
                State = GroceryContributionState.Outstanding,
                CreatedAtUtc = now
            });
            affectedItems.Add(groceryItem);
        }

        foreach (var item in affectedItems)
        {
            if (!GroceryRecipeQuantityRules.TryRecalculate(item, out var quantity))
            {
                return GroceryRecipeServiceSupport.Invalid(
                    "recipe", "The consolidated purchase target exceeds the supported range.");
            }

            item.OutstandingPurchaseQuantity = quantity;
            item.State = GroceryItemState.Outstanding;
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
                GroceryRecipeServiceSupport.Conflict("The grocery list changed during this addition. Review it again."));
        }
        catch (DbUpdateException exception) when (GroceryRecipeServiceSupport.IsGroceryLineConflict(exception))
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("A matching grocery line changed during this addition. Review it again."));
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
                GroceryRecipeServiceSupport.Conflict("The grocery list changed during this addition. Review it again."));
        }
        catch (DbUpdateException exception) when (GroceryMutationPersistence.IsReceiptConflict(exception))
        {
            return await RecoverAsync(
                GroceryRecipeServiceSupport.Conflict("The grocery list changed during this addition. Review it again."));
        }

        return GroceryRecipeServiceSupport.Success(receipt, StatusCodes.Status201Created);

        async Task<GroceryWriteOutcome> RecoverAsync(GroceryWriteOutcome fallback)
        {
            var committed = await GroceryMutationPersistence.FindReceiptAfterRollbackAsync(
                databaseFactory, transaction, ownerId, command.CommandId, cancellationToken);
            if (committed is null)
            {
                return fallback;
            }

            return committed.CommandType == CommandType && committed.RequestFingerprint == fingerprint
                ? GroceryRecipeServiceSupport.Success(committed, StatusCodes.Status201Created)
                : GroceryRecipeServiceSupport.Conflict("This command ID was already used with different content.");
        }
    }
}
