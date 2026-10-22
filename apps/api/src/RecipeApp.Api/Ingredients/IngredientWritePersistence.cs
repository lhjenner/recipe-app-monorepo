using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Ingredients;

internal static class IngredientWritePersistence
{
    public static async Task ValidateReferencesAsync(
        AppDbContext database,
        Guid ownerId,
        IngredientWriteRequest request,
        Dictionary<string, string[]> errors,
        CancellationToken cancellationToken)
    {
        if (request.CategoryId is not null
            && !await database.Categories.AnyAsync(
                item => item.Id == request.CategoryId && item.OwnerUserId == ownerId,
                cancellationToken))
        {
            errors[nameof(request.CategoryId)] = ["Category does not exist."];
        }

        var storeIds = request.StoreMappings.Select(item => item.StoreId).Distinct().ToArray();
        var validStores = await database.Stores
            .Where(item => storeIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (validStores.Count != storeIds.Length)
        {
            errors[nameof(request.StoreMappings)] = ["One or more stores do not exist."];
        }

        var departmentIds = request.StoreMappings
            .Where(item => item.DepartmentId is not null)
            .Select(item => item.DepartmentId!.Value)
            .Distinct()
            .ToArray();
        var validDepartments = await database.Departments
            .Where(item => departmentIds.Contains(item.Id) && item.OwnerUserId == ownerId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (validDepartments.Count != departmentIds.Length)
        {
            errors[nameof(request.StoreMappings)] = ["One or more departments do not exist."];
        }

        var chainIds = request.ProductLinks.Select(item => item.ChainId).Distinct().ToArray();
        var validChains = await database.SupermarketChains
            .Where(item => chainIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        if (validChains.Count != chainIds.Length)
        {
            errors[nameof(request.ProductLinks)] = ["One or more supermarket chains do not exist."];
        }
    }

    public static void AddMappings(
        AppDbContext database,
        Ingredient ingredient,
        IReadOnlyList<IngredientStoreMappingRequest> requests)
    {
        foreach (var request in requests)
        {
            if (request.IsNotStocked || request.DepartmentId is not null)
            {
                database.IngredientStoreMappings.Add(new IngredientStoreMapping
                {
                    IngredientId = ingredient.Id,
                    StoreId = request.StoreId,
                    DepartmentId = request.DepartmentId,
                    IsNotStocked = request.IsNotStocked
                });
            }
        }
    }

    public static void AddProductLinks(
        AppDbContext database,
        Ingredient ingredient,
        IReadOnlyList<IngredientProductLinkRequest> requests)
    {
        for (var index = 0; index < requests.Count; index++)
        {
            var request = requests[index];
            database.ProductLinks.Add(new ProductLink
            {
                Id = Guid.CreateVersion7(),
                IngredientId = ingredient.Id,
                ChainId = request.ChainId,
                Url = request.Url!,
                VariantLabel = CleanOptional(request.VariantLabel),
                Note = CleanOptional(request.Note),
                SupportsBothPricingMethods = request.SupportsBothPricingMethods,
                IsPreferred = request.IsPreferred,
                Position = index
            });
        }
    }

    public static NutritionPer100g? ToNutritionEntity(IngredientNutritionRequest? nutrition)
    {
        if (nutrition is null
            || nutrition == new IngredientNutritionRequest(null, null, null, null, null, null, null, null))
        {
            return null;
        }

        return new NutritionPer100g
        {
            EnergyKcal = nutrition.EnergyKcal,
            ProteinG = nutrition.ProteinG,
            CarbohydrateG = nutrition.CarbohydrateG,
            SugarG = nutrition.SugarG,
            FatG = nutrition.FatG,
            SaturatedFatG = nutrition.SaturatedFatG,
            FibreG = nutrition.FibreG,
            SodiumMg = nutrition.SodiumMg
        };
    }

    public static bool NutritionEquals(
        NutritionPer100g? current,
        IngredientNutritionRequest? request) =>
        current?.EnergyKcal == request?.EnergyKcal
        && current?.ProteinG == request?.ProteinG
        && current?.CarbohydrateG == request?.CarbohydrateG
        && current?.SugarG == request?.SugarG
        && current?.FatG == request?.FatG
        && current?.SaturatedFatG == request?.SaturatedFatG
        && current?.FibreG == request?.FibreG
        && current?.SodiumMg == request?.SodiumMg;

    public static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
