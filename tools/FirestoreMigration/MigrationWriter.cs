using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace FirestoreMigration;

internal sealed class MigrationWriter(AppDbContext db, Guid owner, TargetSnapshot target)
{
    private readonly Dictionary<string, Category> _categories = target.Categories.ToDictionary(row => row.NormalizedName);
    private readonly Dictionary<string, Department> _departments = target.Departments.ToDictionary(row => row.NormalizedName);
    private readonly Dictionary<string, Store> _stores = target.Stores.ToDictionary(row => row.Code);
    private readonly Dictionary<string, SupermarketChain> _chains = target.Chains.ToDictionary(row => row.Code);

    public async Task ApplyAsync(MigrationSource source, IReadOnlyList<IngredientValue> applied,
        CancellationToken cancellationToken)
    {
        foreach (var name in source.Categories) Category(name);
        foreach (var name in source.Departments) Department(name);
        var now = DateTime.UtcNow;
        foreach (var incoming in applied)
        {
            var row = await db.Ingredients.Include(row => row.StoreMappings).Include(row => row.ProductLinks)
                .SingleOrDefaultAsync(row => row.Id == incoming.Id, cancellationToken);
            if (row is null)
            {
                row = new Ingredient
                {
                    Id = incoming.Id, OwnerUserId = owner, Name = incoming.Name,
                    NormalizedName = MigrationNames.Normalize(incoming.Name), CreatedAtUtc = now
                };
                db.Ingredients.Add(row);
            }
            row.Name = incoming.Name;
            row.NormalizedName = MigrationNames.Normalize(incoming.Name);
            row.CategoryId = incoming.Category is null ? null : Category(incoming.Category).Id;
            row.ExcludeFromGroceryList = incoming.ExcludeFromGroceryList;
            row.UpdatedAtUtc = now;
            row.Nutrition = new NutritionPer100g
            {
                EnergyKcal = incoming.Nutrition.EnergyKcal, ProteinG = incoming.Nutrition.ProteinG,
                CarbohydrateG = incoming.Nutrition.CarbohydrateG, SugarG = incoming.Nutrition.SugarG,
                FatG = incoming.Nutrition.FatG, SaturatedFatG = incoming.Nutrition.SaturatedFatG,
                FibreG = incoming.Nutrition.FibreG, SodiumMg = incoming.Nutrition.SodiumMg
            };
            row.NutritionSource = incoming.Nutrition.Source;
            row.NutritionUpdatedAtUtc = incoming.Nutrition.UpdatedAtUtc;
            ApplyMappings(row, incoming);
            await ApplyLinksAsync(row, incoming, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private Category Category(string name)
    {
        var key = MigrationNames.Normalize(name);
        if (_categories.TryGetValue(key, out var category)) return category;
        category = new() { Id = Guid.CreateVersion7(), OwnerUserId = owner,
            Name = MigrationNames.Clean(name), NormalizedName = key };
        _categories.Add(key, category);
        db.Categories.Add(category);
        return category;
    }

    private Department Department(string name)
    {
        var key = MigrationNames.Normalize(name);
        if (_departments.TryGetValue(key, out var department)) return department;
        department = new() { Id = Guid.CreateVersion7(), OwnerUserId = owner,
            Name = MigrationNames.Clean(name), NormalizedName = key };
        _departments.Add(key, department);
        db.Departments.Add(department);
        return department;
    }

    private void ApplyMappings(Ingredient row, IngredientValue incoming)
    {
        var desired = incoming.Mappings.ToDictionary(mapping => _stores[mapping.Store].Id);
        foreach (var mapping in row.StoreMappings.ToArray())
        {
            if (desired.ContainsKey(mapping.StoreId)) continue;
            db.IngredientStoreMappings.Remove(mapping);
            row.StoreMappings.Remove(mapping);
        }
        foreach (var (store, value) in desired)
        {
            var mapping = row.StoreMappings.SingleOrDefault(mapping => mapping.StoreId == store);
            if (mapping is null)
            {
                mapping = new() { IngredientId = row.Id, StoreId = store };
                row.StoreMappings.Add(mapping);
            }
            mapping.IsNotStocked = value.IsNotStocked;
            mapping.DepartmentId = value.Department is null ? null : Department(value.Department).Id;
        }
    }

    private async Task ApplyLinksAsync(Ingredient row, IngredientValue incoming, CancellationToken cancellationToken)
    {
        var desired = incoming.Links.ToDictionary(link => (_chains[link.Chain].Id, link.Position));
        var changed = new List<(ProductLink Row, LinkValue Value)>();
        foreach (var link in row.ProductLinks.ToArray())
        {
            if (!desired.ContainsKey((link.ChainId, link.Position)))
            {
                db.ProductLinks.Remove(link);
                row.ProductLinks.Remove(link);
            }
            else
            {
                var value = desired[(link.ChainId, link.Position)];
                changed.Add((link, value));
            }
        }
        // Release the partial unique preferred-link index before switching preference.
        foreach (var (link, value) in changed.Where(pair => pair.Row.IsPreferred && !pair.Value.IsPreferred))
            link.IsPreferred = false;
        await db.SaveChangesAsync(cancellationToken);
        foreach (var value in incoming.Links)
        {
            var chain = _chains[value.Chain].Id;
            var link = row.ProductLinks.SingleOrDefault(link => link.ChainId == chain && link.Position == value.Position);
            if (link is null)
            {
                link = new() { Id = Guid.CreateVersion7(), IngredientId = row.Id, ChainId = chain,
                    Position = value.Position, Url = value.Url };
                row.ProductLinks.Add(link);
            }
            link.Url = value.Url;
            link.VariantLabel = value.VariantLabel;
            link.Note = value.Note;
            link.SupportsBothPricingMethods = value.SupportsBothPricingMethods;
            link.IsPreferred = value.IsPreferred;
        }
    }
}
