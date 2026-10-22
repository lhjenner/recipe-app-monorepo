using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

public sealed record TargetSnapshot(List<Ingredient> Ingredients, List<Category> Categories,
    List<Department> Departments, List<Store> Stores, List<SupermarketChain> Chains,
    List<Guid> OwnershipConflicts, string Fingerprint)
{
    public static async Task<TargetSnapshot> ReadAsync(AppDbContext db, Guid owner,
        MigrationSource source, CancellationToken cancellationToken)
    {
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any() || db.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Database schema is not current. Apply the API's EF migrations first.");
        if (!await db.Users.AnyAsync(user => user.Id == owner, cancellationToken))
            throw new InvalidOperationException("Destination owner does not exist. Register the account first.");
        var ingredients = await db.Ingredients.AsNoTracking().Where(row => row.OwnerUserId == owner)
            .Include(row => row.Category)
            .Include(row => row.StoreMappings).ThenInclude(row => row.Store)
            .Include(row => row.StoreMappings).ThenInclude(row => row.Department)
            .Include(row => row.ProductLinks).ThenInclude(row => row.Chain)
            .AsSplitQuery().ToListAsync(cancellationToken);
        var ids = source.SourceIds.ToArray();
        var conflicts = await db.Ingredients.AsNoTracking()
            .Where(row => row.OwnerUserId != owner && ids.Contains(row.Id))
            .Select(row => row.Id).ToListAsync(cancellationToken);
        var categories = await db.Categories.AsNoTracking().Where(row => row.OwnerUserId == owner)
            .ToListAsync(cancellationToken);
        var departments = await db.Departments.AsNoTracking().Where(row => row.OwnerUserId == owner)
            .ToListAsync(cancellationToken);
        var stores = await db.Stores.AsNoTracking().ToListAsync(cancellationToken);
        var chains = await db.SupermarketChains.AsNoTracking().ToListAsync(cancellationToken);
        // xmin detects even change-then-revert edits and child changes that do not update the parent.
        // The deliberately conservative read-set also rejects changes by other owners.
        var versions = await db.Database.SqlQueryRaw<string>("""
            SELECT 'ingredient:' || "Id"::text || ':' || xmin::text AS "Value" FROM "Ingredients"
            UNION ALL SELECT 'category:' || "Id"::text || ':' || xmin::text FROM "Categories"
            UNION ALL SELECT 'department:' || "Id"::text || ':' || xmin::text FROM "Departments"
            UNION ALL SELECT 'store:' || "Id"::text || ':' || xmin::text FROM "Stores"
            UNION ALL SELECT 'chain:' || "Id"::text || ':' || xmin::text FROM "SupermarketChains"
            UNION ALL SELECT 'link:' || "Id"::text || ':' || xmin::text FROM "ProductLinks"
            UNION ALL SELECT 'mapping:' || "IngredientId"::text || ':' || "StoreId"::text || ':' || xmin::text
                FROM "IngredientStoreMappings"
            """).ToListAsync(cancellationToken);
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', versions.Order(StringComparer.Ordinal)))));
        return new(ingredients, categories, departments, stores, chains, conflicts, fingerprint);
    }

    public static IngredientValue Value(Ingredient row) => new(row.Id, row.Name, row.Category?.Name,
        row.ExcludeFromGroceryList,
        row.StoreMappings.Select(mapping => new MappingValue(mapping.Store!.Code,
            mapping.Department?.Name, mapping.IsNotStocked)).ToArray(),
        row.ProductLinks.Select(link => new LinkValue(link.Chain!.Code, link.Position, link.Url,
            link.VariantLabel, link.Note, link.SupportsBothPricingMethods, link.IsPreferred)).ToArray(),
        new(row.Nutrition?.EnergyKcal, row.Nutrition?.ProteinG, row.Nutrition?.CarbohydrateG,
            row.Nutrition?.SugarG, row.Nutrition?.FatG, row.Nutrition?.SaturatedFatG,
            row.Nutrition?.FibreG, row.Nutrition?.SodiumMg, row.NutritionSource, row.NutritionUpdatedAtUtc));
}
