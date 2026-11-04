using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Auth;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Recipes;

[Binding]
public sealed class RecipeListSteps(ApiAuthContext context)
{
    [When("I request the recipe list without signing in")]
    public async Task RequestWithoutSigningInAsync() =>
        context.Response = await context.Client.GetAsync("/api/recipes");

    [When("I request the default recipe list")]
    public async Task RequestDefaultListAsync() =>
        context.Response = await context.Client.GetAsync("/api/recipes");

    [When("I request recipe page {int} with page size {int}")]
    public async Task RequestPageAsync(int page, int pageSize) =>
        context.Response = await context.Client.GetAsync(
            $"/api/recipes?page={page}&pageSize={pageSize}");

    [When("I search recipes for {string}")]
    public async Task SearchAsync(string search) =>
        context.Response = await context.Client.GetAsync(
            $"/api/recipes?search={Uri.EscapeDataString(search)}");

    [When("I search recipes for a 201 character query")]
    public Task SearchLongQueryAsync() => SearchAsync(new string('x', 201));

    [When("I search recipes for a backslash")]
    public Task SearchBackslashAsync() => SearchAsync("\\");

    [Given("the user owns {int} active recipes")]
    public Task SeedActiveRecipesAsync(int count) =>
        SeedCurrentUserRecipesAsync(Enumerable.Range(1, count)
            .Select(index => (Name: $"Recipe {index:000}", Archived: false)));

    [Given("the user owns recipes:")]
    public Task SeedRecipesAsync(Table table) =>
        SeedCurrentUserRecipesAsync(table.Rows
            .Select(row => (Name: row["name"], Archived: false)));

    [Given("the user owns one active recipe named {string}")]
    public Task SeedActiveRecipeAsync(string name) =>
        SeedCurrentUserRecipesAsync([(name, false)]);

    [Given("the user owns one archived recipe named {string}")]
    public Task SeedArchivedRecipeAsync(string name) =>
        SeedCurrentUserRecipesAsync([(name, true)]);

    [Given("an archived and grocery-excluded ingredient named {string} is linked twice to {string}")]
    public async Task SeedRecipeWithArchivedIngredientAsync(string ingredientName, string recipeName)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.AsNoTracking().SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());
        var ingredient = new Ingredient
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = ingredientName,
            NormalizedName = ingredientName.ToLowerInvariant(),
            ExcludeFromGroceryList = true,
            ArchivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var recipe = CreateRecipe(owner.Id, recipeName, archived: false);
        var step = new RecipeStep
        {
            OwnerUserId = owner.Id,
            Position = 0,
            Instructions = ""
        };
        step.Items.Add(CreateIngredientItem(owner.Id, ingredient.Id, 0));
        step.Items.Add(CreateIngredientItem(owner.Id, ingredient.Id, 1));
        recipe.Steps.Add(step);
        db.AddRange(ingredient, recipe);
        await db.SaveChangesAsync();
    }

    [Given("a recipe named {string} has a heading item named {string}")]
    public async Task SeedRecipeWithHeadingAsync(string recipeName, string heading)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.AsNoTracking().SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());
        var recipe = CreateRecipe(owner.Id, recipeName, archived: false);
        var step = new RecipeStep
        {
            OwnerUserId = owner.Id,
            Position = 0,
            Instructions = ""
        };
        step.Items.Add(new RecipeStepItem
        {
            OwnerUserId = owner.Id,
            Position = 0,
            Kind = RecipeStepItemKind.Heading,
            Text = heading
        });
        recipe.Steps.Add(step);
        db.Recipes.Add(recipe);
        await db.SaveChangesAsync();
    }

    [Given("another user owns an active recipe named {string}")]
    public async Task SeedOtherUserRecipeAsync(string name)
    {
        const string email = "recipe-list-other-user@example.test";
        const string password = "Synthetic-Password-123!";
        await context.SeedExistingAccountAsync(email, password);
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        db.Recipes.Add(CreateRecipe(owner.Id, name, archived: false));
        await db.SaveChangesAsync();
    }

    [Then("the recipe list response is unauthorized")]
    public void AssertUnauthorized() =>
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Then("the recipe list contains {int} items, {int} total, on page {int}")]
    public async Task AssertPageAsync(int itemCount, int totalCount, int page)
    {
        using var document = await ReadResponseAsync();
        var result = document.RootElement;
        result.GetProperty("items").GetArrayLength().Should().Be(itemCount);
        result.GetProperty("totalCount").GetInt32().Should().Be(totalCount);
        result.GetProperty("page").GetInt32().Should().Be(page);
        result.GetProperty("pageSize").GetInt32().Should().Be(25);
        foreach (var item in result.GetProperty("items").EnumerateArray())
        {
            item.EnumerateObject().Select(property => property.Name)
                .Should().BeEquivalentTo("id", "name");
            Guid.TryParse(item.GetProperty("id").GetString(), out var id).Should().BeTrue();
            id.Version.Should().Be(7);
        }
    }

    [Then("the recipe names are sorted by normalized name")]
    public async Task AssertSortedAsync()
    {
        using var document = await ReadResponseAsync();
        var names = document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();
        names.Should().BeInAscendingOrder(name => name);
    }

    [Then("the recipe list contains the names {string}")]
    public async Task AssertNamesAsync(string expectedNames)
    {
        using var document = await ReadResponseAsync();
        document.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .Should().BeEquivalentTo(expectedNames.Split(','));
    }

    private async Task SeedCurrentUserRecipesAsync(IEnumerable<(string Name, bool Archived)> recipes)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var email = context.Email.Trim().ToLowerInvariant();
        var owner = await db.Users.AsNoTracking().SingleAsync(user => user.Email == email);
        db.Recipes.AddRange(recipes.Select(recipe =>
            CreateRecipe(owner.Id, recipe.Name, recipe.Archived)));
        await db.SaveChangesAsync();
    }

    private static Recipe CreateRecipe(Guid ownerUserId, string name, bool archived) =>
        new()
        {
            OwnerUserId = ownerUserId,
            Name = name,
            NormalizedName = string.Join(
                    ' ',
                    name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant(),
            IsArchived = archived,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private static RecipeStepItem CreateIngredientItem(Guid ownerUserId, Guid ingredientId, int position) =>
        new()
        {
            OwnerUserId = ownerUserId,
            Position = position,
            Kind = RecipeStepItemKind.Ingredient,
            IngredientId = ingredientId,
            Text = "Tofu"
        };

    private async Task<JsonDocument> ReadResponseAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
    }
}
