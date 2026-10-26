using Microsoft.EntityFrameworkCore;
using Npgsql;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Ingredients;

public sealed class IngredientWriteService(
    IDbContextFactory<AppDbContext> databaseFactory,
    IIngredientReadService readService,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IIngredientWriteService
{
    public Task<IngredientWriteOutcome> CreateAsync(
        IngredientWriteRequest request,
        CancellationToken cancellationToken) =>
        SaveAsync(null, request, cancellationToken);

    public Task<IngredientWriteOutcome> UpdateAsync(
        Guid id,
        IngredientWriteRequest request,
        CancellationToken cancellationToken) =>
        SaveAsync(id, request, cancellationToken);

    public Task<IngredientArchiveOutcome> ArchiveAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        SetArchivedAsync(id, true, cancellationToken);

    public Task<IngredientArchiveOutcome> RestoreAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        SetArchivedAsync(id, false, cancellationToken);

    private async Task<IngredientArchiveOutcome> SetArchivedAsync(
        Guid id,
        bool archived,
        CancellationToken cancellationToken)
    {
        var ownerId = currentUser.UserId;
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var ingredient = await database.Ingredients
            .SingleOrDefaultAsync(
                item => item.Id == id && item.OwnerUserId == ownerId,
                cancellationToken);
        if (ingredient is null)
        {
            return new IngredientArchiveOutcome(StatusCodes.Status404NotFound);
        }

        if ((ingredient.ArchivedAtUtc is not null) == archived)
        {
            return new IngredientArchiveOutcome(StatusCodes.Status204NoContent);
        }

        if (!archived && await database.Ingredients.AnyAsync(
                item => item.OwnerUserId == ownerId
                    && item.Id != id
                    && item.NormalizedName == ingredient.NormalizedName,
                cancellationToken))
        {
            return new IngredientArchiveOutcome(
                StatusCodes.Status409Conflict,
                "An ingredient with this name already exists",
                "An active ingredient with this name already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        ingredient.ArchivedAtUtc = archived ? now : null;
        ingredient.UpdatedAtUtc = now;
        database.Entry(ingredient).Property(item => item.UpdatedAtUtc).IsModified = true;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new IngredientArchiveOutcome(
                StatusCodes.Status409Conflict,
                "Conflict",
                "This ingredient was changed elsewhere.");
        }
        catch (DbUpdateException exception) when (IsIngredientNameConflict(exception))
        {
            return new IngredientArchiveOutcome(
                StatusCodes.Status409Conflict,
                "An ingredient with this name already exists",
                "An active ingredient with this name already exists.");
        }

        return new IngredientArchiveOutcome(StatusCodes.Status204NoContent);
    }

    private async Task<IngredientWriteOutcome> SaveAsync(
        Guid? id,
        IngredientWriteRequest request,
        CancellationToken cancellationToken)
    {
        var errors = IngredientWriteValidator.Validate(request);
        if (id is not null && request.Version == 0)
        {
            errors[nameof(request.Version)] = ["Version is required for an update."];
        }

        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var ownerId = currentUser.UserId;
        var normalizedName = IngredientWriteValidator.Normalize(request.Name);
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);

        var ingredient = id is null
            ? null
            : await database.Ingredients
                .Include(item => item.StoreMappings)
                .Include(item => item.ProductLinks)
                .SingleOrDefaultAsync(
                    item => item.Id == id && item.OwnerUserId == ownerId,
                    cancellationToken);
        if (id is not null && ingredient is null)
        {
            return new IngredientWriteOutcome(null, StatusCodes.Status404NotFound);
        }

        if (ingredient is not null && ingredient.Version != request.Version)
        {
            return Conflict("This ingredient was changed elsewhere.");
        }

        var nameConflict = await database.Ingredients
            .Where(item => item.OwnerUserId == ownerId
                && item.NormalizedName == normalizedName
                && item.Id != id)
            .Select(item => new { item.Id, item.ArchivedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);
        if (nameConflict is not null)
        {
            return nameConflict.ArchivedAtUtc is not null
                ? new IngredientWriteOutcome(
                    null,
                    StatusCodes.Status409Conflict,
                    "An archived ingredient with this name exists",
                    $"Restore the archived ingredient before using this name. Ingredient ID: {nameConflict.Id}.",
                    nameConflict.Id)
                : new IngredientWriteOutcome(
                    null,
                    StatusCodes.Status409Conflict,
                    "An ingredient with this name already exists",
                    "An ingredient with this name already exists.");
        }

        await IngredientWritePersistence.ValidateReferencesAsync(
            database, ownerId, request, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return Invalid(errors);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var isNew = ingredient is null;
        ingredient ??= new Ingredient
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerId,
            Name = string.Empty,
            NormalizedName = string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var nutritionChanged = !IngredientWritePersistence.NutritionEquals(
            ingredient.Nutrition, request.Nutrition);
        ingredient.Name = IngredientWriteValidator.Clean(request.Name);
        ingredient.NormalizedName = normalizedName;
        ingredient.CategoryId = request.CategoryId;
        ingredient.ExcludeFromGroceryList = request.ExcludeFromGroceryList;
        ingredient.Nutrition = IngredientWritePersistence.ToNutritionEntity(request.Nutrition);
        ingredient.NutritionSource = IngredientWritePersistence.CleanOptional(request.NutritionSource);
        if (nutritionChanged)
        {
            ingredient.NutritionUpdatedAtUtc = now;
        }

        try
        {
            if (isNew)
            {
                database.Ingredients.Add(ingredient);
                IngredientWritePersistence.AddMappings(database, ingredient, request.StoreMappings);
                IngredientWritePersistence.AddProductLinks(database, ingredient, request.ProductLinks);
                await database.SaveChangesAsync(cancellationToken);
            }
            else
            {
                ingredient.UpdatedAtUtc = now;
                database.Entry(ingredient).Property(item => item.UpdatedAtUtc).IsModified = true;
                database.Entry(ingredient).Property(item => item.Version).OriginalValue = request.Version;
                database.IngredientStoreMappings.RemoveRange(ingredient.StoreMappings);
                database.ProductLinks.RemoveRange(ingredient.ProductLinks);
                ingredient.StoreMappings.Clear();
                ingredient.ProductLinks.Clear();

                await using var transaction =
                    await database.Database.BeginTransactionAsync(cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
                IngredientWritePersistence.AddMappings(database, ingredient, request.StoreMappings);
                IngredientWritePersistence.AddProductLinks(database, ingredient, request.ProductLinks);
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("This ingredient was changed elsewhere.");
        }
        catch (DbUpdateException exception) when (IsIngredientNameConflict(exception))
        {
            return await GetNameConflictAsync(ownerId, normalizedName, cancellationToken);
        }

        var detail = await readService.GetByIdAsync(ingredient.Id, cancellationToken);
        return new IngredientWriteOutcome(
            detail,
            isNew ? StatusCodes.Status201Created : StatusCodes.Status200OK);
    }

    private async Task<IngredientWriteOutcome> GetNameConflictAsync(
        Guid ownerId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var conflict = await database.Ingredients.AsNoTracking()
            .Where(item => item.OwnerUserId == ownerId && item.NormalizedName == normalizedName)
            .Select(item => new { item.Id, item.ArchivedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);
        return conflict?.ArchivedAtUtc is not null
            ? new IngredientWriteOutcome(
                null,
                StatusCodes.Status409Conflict,
                "An archived ingredient with this name exists",
                $"Restore the archived ingredient before using this name. Ingredient ID: {conflict.Id}.",
                conflict.Id)
            : new IngredientWriteOutcome(
                null,
                StatusCodes.Status409Conflict,
                "An ingredient with this name already exists",
                "An ingredient with this name already exists.");
    }

    private static bool IsIngredientNameConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Ingredients_OwnerUserId_NormalizedName"
        };

    private static IngredientWriteOutcome Invalid(Dictionary<string, string[]> errors) =>
        new(null, StatusCodes.Status400BadRequest, Errors: errors);

    private static IngredientWriteOutcome Conflict(string detail) =>
        new(null, StatusCodes.Status409Conflict, "Conflict", detail);
}
