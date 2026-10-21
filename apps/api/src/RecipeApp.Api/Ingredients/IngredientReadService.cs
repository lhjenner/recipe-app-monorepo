using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Ingredients;

public sealed class IngredientReadService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser) : IIngredientReadService
{
    public async Task<IngredientListResponse?> GetListAsync(
        IngredientListQuery query,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var ownerUserId = currentUser.UserId;
        var ingredientsQuery = database.Ingredients
            .AsNoTracking()
            .Where(ingredient => ingredient.OwnerUserId == ownerUserId
                && (ingredient.ArchivedAtUtc != null) == query.Archived);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            ingredientsQuery = ingredientsQuery.Where(ingredient =>
                EF.Functions.ILike(ingredient.Name, pattern, "\\"));
        }

        var totalCount = await ingredientsQuery.CountAsync(cancellationToken);
        var pageCount = Math.Max(1, (int)Math.Ceiling(totalCount / (double)query.PageSize));
        if (query.Page > pageCount)
        {
            return null;
        }

        var stores = await database.Stores
            .AsNoTracking()
            .OrderBy(store => store.DisplayOrder)
            .Select(store => new StoreListItem(store.Id, store.Code))
            .ToListAsync(cancellationToken);
        var ingredients = await ingredientsQuery
            .OrderBy(ingredient => ingredient.NormalizedName)
            .ThenBy(ingredient => ingredient.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(ingredient => ingredient.Category)
            .Include(ingredient => ingredient.StoreMappings)
                .ThenInclude(mapping => mapping.Department)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var items = ingredients.Select(ingredient => new IngredientSummaryResponse(
            ingredient.Id,
            ingredient.Name,
            ingredient.Category?.Name,
            ingredient.ExcludeFromGroceryList,
            ingredient.ArchivedAtUtc is not null,
            stores.Select(store =>
            {
                var mapping = ingredient.StoreMappings
                    .SingleOrDefault(item => item.StoreId == store.Id);
                return new IngredientStoreSummaryResponse(
                    store.Code,
                    GetState(mapping),
                    mapping?.Department?.Name);
            }).ToArray())).ToArray();

        return new IngredientListResponse(items, totalCount, query.Page, query.PageSize);
    }

    public async Task<IngredientDetailResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var database = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var ingredient = await database.Ingredients
            .AsNoTracking()
            .Where(item => item.Id == id && item.OwnerUserId == currentUser.UserId)
            .Include(item => item.Category)
            .Include(item => item.StoreMappings)
                .ThenInclude(mapping => mapping.Department)
            .Include(item => item.ProductLinks)
                .ThenInclude(link => link.Chain)
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);
        if (ingredient is null)
        {
            return null;
        }

        var stores = await database.Stores
            .AsNoTracking()
            .OrderBy(store => store.DisplayOrder)
            .ToListAsync(cancellationToken);
        var storeMappings = stores.Select(store =>
        {
            var mapping = ingredient.StoreMappings
                .SingleOrDefault(item => item.StoreId == store.Id);
            return new IngredientStoreDetailResponse(
                store.Id,
                store.Code,
                store.Name,
                GetState(mapping),
                mapping?.DepartmentId,
                mapping?.Department?.Name);
        }).ToArray();
        var productLinks = ingredient.ProductLinks
            .OrderBy(link => link.Position)
            .Select(link => new IngredientProductLinkResponse(
                link.Id,
                link.Chain!.Code,
                link.Chain.Name,
                link.Url,
                link.VariantLabel,
                link.Note,
                link.SupportsBothPricingMethods,
                link.IsPreferred,
                link.Position))
            .ToArray();

        return new IngredientDetailResponse(
            ingredient.Id,
            ingredient.Name,
            ingredient.CategoryId,
            ingredient.Category?.Name,
            ingredient.ExcludeFromGroceryList,
            ingredient.Nutrition is null ? null : MapNutrition(ingredient.Nutrition),
            ingredient.NutritionSource,
            ToDateTimeOffset(ingredient.NutritionUpdatedAtUtc),
            ToDateTimeOffset(ingredient.CreatedAtUtc)!.Value,
            ToDateTimeOffset(ingredient.UpdatedAtUtc)!.Value,
            ToDateTimeOffset(ingredient.ArchivedAtUtc),
            ingredient.Version,
            storeMappings,
            productLinks);
    }

    private static IngredientMappingState GetState(IngredientStoreMapping? mapping) =>
        mapping switch
        {
            { IsNotStocked: true } => IngredientMappingState.NotStocked,
            { DepartmentId: not null } => IngredientMappingState.Mapped,
            _ => IngredientMappingState.Unmapped
        };

    private static IngredientNutritionResponse MapNutrition(NutritionPer100g nutrition) =>
        new(
            nutrition.EnergyKcal,
            nutrition.ProteinG,
            nutrition.CarbohydrateG,
            nutrition.SugarG,
            nutrition.FatG,
            nutrition.SaturatedFatG,
            nutrition.FibreG,
            nutrition.SodiumMg);

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private sealed record StoreListItem(Guid Id, string Code);
}
