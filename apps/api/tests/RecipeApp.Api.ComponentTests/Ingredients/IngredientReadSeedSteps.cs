using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Ingredients;

[Binding]
public sealed class IngredientReadSeedSteps(ApiAuthContext context)
{
    [Given("the user owns {int} active ingredients")]
    public Task GivenTheUserOwnsActiveIngredients(int count) =>
        SeedIngredientsAsync(Enumerable.Range(1, count)
            .Select(index => (Name: $"Ingredient {index:000}", Archived: false)));

    [Given("the user owns one active ingredient and no other ingredients")]
    public Task GivenTheUserOwnsOneActiveIngredient() =>
        SeedIngredientsAsync([("Only Ingredient", false)]);

    [Given("the user owns ingredients named {string}, {string}, and {string}")]
    public Task GivenTheUserOwnsIngredientsNamed(string first, string second, string third) =>
        SeedIngredientsAsync([
            (first, false),
            (second, false),
            (third, false)
        ]);

    [Given("the user owns an ingredient named {string}")]
    public async Task GivenTheUserOwnsAnIngredientNamed(string name)
    {
        var ingredients = await SeedIngredientsAsync([(name, false)]);
        context.ScenarioIngredientId = ingredients[0].Id;
    }

    [Given("the user owns an archived ingredient named {string}")]
    public async Task GivenTheUserOwnsAnArchivedIngredientNamed(string name)
    {
        var ingredients = await SeedIngredientsAsync([(name, true)]);
        context.ScenarioIngredientId = ingredients[0].Id;
    }

    [Given("another user owns an ingredient named {string}")]
    public async Task GivenAnotherUserOwnsAnIngredientNamed(string name)
    {
        const string email = "another-lookup-user@example.com";
        await context.SeedExistingAccountAsync(email, "password123");
        await using var scope = context.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await database.Users.AsNoTracking()
            .SingleAsync(user => user.Email == email);
        database.Ingredients.Add(CreateIngredient(owner.Id, name));
        await database.SaveChangesAsync();
    }

    [Given("the user owns one active ingredient and one archived ingredient")]
    public Task GivenTheUserOwnsOneActiveAndOneArchivedIngredient() =>
        SeedIngredientsAsync(
        [
            ("Active Ingredient", false),
            ("Archived Ingredient", true)
        ]);

    [Given("the user owns an ingredient with category, nutrition, product link, and store mappings")]
    public async Task GivenTheUserOwnsAnIngredientWithRelatedData()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await database.Users.AsNoTracking()
            .SingleAsync(user => user.Email == context.Email.Trim().ToLowerInvariant());
        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "Produce",
            NormalizedName = "produce"
        };
        var department = new Department
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "Fruit and Vegetables",
            NormalizedName = "fruit and vegetables"
        };
        var chain = await database.SupermarketChains
            .Include(item => item.Stores)
            .SingleAsync(item => item.Code == "new-world");
        var ingredient = CreateIngredient(owner.Id, "Golden Apple");
        ingredient.Category = category;
        ingredient.CategoryId = category.Id;
        ingredient.Nutrition = new NutritionPer100g
        {
            EnergyKcal = 52,
            CarbohydrateG = 14,
            SugarG = 10
        };
        ingredient.StoreMappings.Add(new IngredientStoreMapping
        {
            IngredientId = ingredient.Id,
            StoreId = chain.Stores.Single().Id,
            DepartmentId = department.Id,
            Department = department
        });
        ingredient.StoreMappings.Add(new IngredientStoreMapping
        {
            IngredientId = ingredient.Id,
            StoreId = Guid.Parse("b4df52fd-667a-41d8-9a9b-9d6e3bc8c87c"),
            IsNotStocked = true
        });
        ingredient.ProductLinks.Add(new ProductLink
        {
            Id = Guid.CreateVersion7(),
            IngredientId = ingredient.Id,
            ChainId = chain.Id,
            Url = "https://example.com/apples",
            IsPreferred = true,
            Position = 0
        });
        database.AddRange(category, department, ingredient);
        await database.SaveChangesAsync();
        context.ScenarioIngredientId = ingredient.Id;
    }

    private async Task<List<Ingredient>> SeedIngredientsAsync(
        IEnumerable<(string Name, bool Archived)> seed)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await database.Users.AsNoTracking()
            .SingleAsync(user => user.Email == context.Email.Trim().ToLowerInvariant());
        var now = DateTime.UtcNow;
        var ingredients = seed.Select(item =>
        {
            var ingredient = CreateIngredient(owner.Id, item.Name);
            if (item.Archived)
            {
                ingredient.ArchivedAtUtc = now;
            }

            return ingredient;
        }).ToList();
        database.Ingredients.AddRange(ingredients);
        await database.SaveChangesAsync();
        return ingredients;
    }

    private static Ingredient CreateIngredient(Guid ownerUserId, string name) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerUserId,
            Name = name,
            NormalizedName = string.Join(
                    ' ',
                    name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
}
