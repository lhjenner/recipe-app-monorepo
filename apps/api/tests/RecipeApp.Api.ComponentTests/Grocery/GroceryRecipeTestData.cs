using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeApp.Api.Auth;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

internal static class GroceryRecipeTestData
{
    public static async Task<User> CurrentOwnerAsync(ApiAuthContext context, AppDbContext db) =>
        await db.Users.AsNoTracking().SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());

    public static async Task<JsonDocument> ReadListAsync(ApiAuthContext context)
    {
        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    public static async Task AddRecipeAsync(
        ApiAuthContext context,
        Guid recipeId,
        uint listVersion,
        uint recipeVersion,
        bool confirmRepeat)
    {
        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync(
            $"/api/grocery-list/recipes/{recipeId}",
            new GroceryRecipeAddCommand(
                Guid.CreateVersion7(),
                listVersion,
                recipeVersion,
                confirmRepeat));
    }

    public static Ingredient CreateIngredient(
        Guid ownerId,
        string name,
        DateTime now,
        bool archived = false,
        bool excluded = false) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = ownerId,
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            ExcludeFromGroceryList = excluded,
            ArchivedAtUtc = archived ? now : null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    public static Recipe CreateRecipe(Guid ownerId, string name, DateTime now) =>
        new()
        {
            OwnerUserId = ownerId,
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    public static RecipeStep CreateStep(Guid ownerId, int position) =>
        new()
        {
            OwnerUserId = ownerId,
            Position = position,
            Instructions = "Prepare the ingredients."
        };

    public static RecipeStepItem CreateIngredientItem(
        Guid ownerId,
        Guid ingredientId,
        int position,
        string text,
        string amount,
        string unit) =>
        new()
        {
            OwnerUserId = ownerId,
            Position = position,
            Kind = RecipeStepItemKind.Ingredient,
            IngredientId = ingredientId,
            Text = text,
            QuantityText = amount,
            UnitText = unit
        };

    public static RecipeStepItem CreateTypedItem(
        Guid ownerId,
        int position,
        RecipeStepItemKind kind,
        string text) =>
        new()
        {
            OwnerUserId = ownerId,
            Position = position,
            Kind = kind,
            Text = text
        };
}
