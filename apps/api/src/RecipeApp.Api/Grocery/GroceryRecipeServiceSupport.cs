using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Grocery;

internal static class GroceryRecipeServiceSupport
{
    internal sealed record EligibleRecipeItem(
        RecipeStep Step,
        RecipeStepItem Item,
        QuantityInterpretation Interpretation);

    public static async Task<Recipe?> LoadRecipeAsync(
        AppDbContext database,
        Guid recipeId,
        Guid ownerId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        var query = database.Recipes.AsQueryable();
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query
            .Where(value => value.Id == recipeId
                && value.OwnerUserId == ownerId
                && !value.IsArchived)
            .Include(value => value.Steps)
                .ThenInclude(value => value.Items)
                    .ThenInclude(value => value.Ingredient)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
    }

    public static async Task LockRecipeAsync(
        AppDbContext database,
        Guid recipeId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Recipes\" WHERE \"Id\" = {recipeId} AND \"OwnerUserId\" = {ownerId} FOR SHARE",
            cancellationToken);
    }

    public static async Task LockRecipeIngredientsAsync(
        AppDbContext database,
        Guid recipeId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"""
            SELECT 1
            FROM "Ingredients"
            WHERE "OwnerUserId" = {ownerId}
                AND "Id" IN (
                    SELECT item."IngredientId"
                    FROM "RecipeSteps" AS step
                    INNER JOIN "RecipeStepItems" AS item
                        ON item."RecipeStepId" = step."Id"
                        AND item."OwnerUserId" = step."OwnerUserId"
                    WHERE step."RecipeId" = {recipeId}
                        AND step."OwnerUserId" = {ownerId}
                        AND item."Kind" = 0
                )
            FOR SHARE
            """,
            cancellationToken);
    }

    public static bool HasMissingIngredientReference(Recipe recipe) =>
        recipe.Steps.SelectMany(step => step.Items)
            .Any(item => item.Kind == RecipeStepItemKind.Ingredient && item.Ingredient is null);

    public static List<EligibleRecipeItem> GetEligibleItems(Recipe recipe) =>
        recipe.Steps
            .OrderBy(step => step.Position)
            .SelectMany(step => step.Items
                .Where(item => item.Kind == RecipeStepItemKind.Ingredient
                    && item.Ingredient?.ExcludeFromGroceryList == false)
                .Select(item => new EligibleRecipeItem(
                    step,
                    item,
                    QuantityInterpreter.Interpret(item.QuantityText, item.UnitText))))
            .ToList();

    public static string GetConsolidationKey(EligibleRecipeItem entry) =>
        entry.Interpretation.Dimension is { } dimension
            ? GroceryConsolidationKey.ForIngredient(
                entry.Item.IngredientId!.Value, dimension, entry.Interpretation.CountLabel)
            : GroceryConsolidationKey.ForUnresolvedIngredient(
                entry.Item.IngredientId!.Value, entry.Item.UnitText);

    public static string GetPurchaseUnit(QuantityInterpretation interpretation) =>
        interpretation.Dimension == QuantityDimension.Count
            ? interpretation.CountLabel!
            : "item";

    public static string TargetKey(string consolidationKey, string purchaseUnit) =>
        $"{consolidationKey}:{purchaseUnit.ToLowerInvariant()}";

    public static Task<bool> HasOutstandingAdditionAsync(
        AppDbContext database,
        Guid recipeId,
        Guid ownerId,
        Guid listId,
        CancellationToken cancellationToken) =>
        database.GroceryRecipeAdditions
            .AnyAsync(addition => addition.RecipeId == recipeId
                && addition.OwnerUserId == ownerId
                && addition.GroceryListId == listId
                && addition.Contributions.Any(contribution =>
                    contribution.State == GroceryContributionState.Outstanding),
                cancellationToken);

    public static async Task<GroceryWriteOutcome?> FindReplayAsync(
        AppDbContext database,
        GroceryList list,
        Guid ownerId,
        Guid commandId,
        string fingerprint,
        string commandType,
        CancellationToken cancellationToken)
    {
        var receipt = await database.GroceryCommandReceipts.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerUserId == ownerId
                && value.GroceryListId == list.Id
                && value.CommandId == commandId,
                cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        return receipt.CommandType == commandType && receipt.RequestFingerprint == fingerprint
            ? Success(receipt, commandType == "recipe-add"
                ? StatusCodes.Status201Created
                : StatusCodes.Status200OK)
            : Conflict("This command ID was already used with different content.");
    }

    public static GroceryWriteOutcome Success(GroceryCommandReceipt receipt, int statusCode)
    {
        var response = JsonSerializer.Deserialize<GroceryCommandReceiptResponse>(receipt.ResultJson);
        return response is null
            ? Conflict("The saved grocery command receipt is invalid.")
            : new GroceryWriteOutcome(response, statusCode);
    }

    public static GroceryWriteOutcome NotFound() =>
        new(null, StatusCodes.Status404NotFound);

    public static GroceryWriteOutcome Invalid(string field, string message) =>
        new(null, StatusCodes.Status400BadRequest, "Validation failed",
            Errors: new Dictionary<string, string[]> { [field] = [message] });

    public static GroceryWriteOutcome Conflict(string detail) =>
        new(null, StatusCodes.Status409Conflict, "Conflict", detail);

    public static bool IsOwnerListConflict(DbUpdateException exception) =>
        IsUniqueViolation(exception, "IX_GroceryLists_OwnerUserId");

    public static bool IsGroceryLineConflict(DbUpdateException exception) =>
        IsUniqueViolation(exception, "IX_GroceryItems_ActivePurchaseLineKey");

    private static bool IsUniqueViolation(DbUpdateException exception, string constraintName) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        } postgres && postgres.ConstraintName == constraintName;
}
