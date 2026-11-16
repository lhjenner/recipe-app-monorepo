using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Auth;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.ComponentTests.Recipes;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryRecipeFloorSteps(ApiAuthContext context)
{
    private Guid _itemId;
    private decimal _originalPurchaseQuantity;

    [Given("the owner has a measured Recipe requirement and manual grocery demand")]
    public async Task SeedMeasuredRecipeDemandAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());
        var now = DateTime.UtcNow;
        var recipeId = Guid.CreateVersion7();
        var list = new GroceryList
        {
            OwnerUserId = owner.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var recipe = new Recipe
        {
            Id = recipeId,
            OwnerUserId = owner.Id,
            Name = "Recipe demand floor",
            NormalizedName = "recipe demand floor",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var addition = new GroceryRecipeAddition
        {
            GroceryList = list,
            OwnerUserId = owner.Id,
            RecipeId = recipeId,
            RecipeNameSnapshot = recipe.Name,
            RecipeVersion = 1,
            AddedAtUtc = now
        };
        var item = new GroceryItem
        {
            GroceryList = list,
            OwnerUserId = owner.Id,
            NameSnapshot = "Oats",
            NormalizedName = "oats",
            ConsolidationKey = GroceryConsolidationKey.ForName("oats", QuantityDimension.Mass, null),
            Dimension = QuantityDimension.Mass,
            PurchaseUnit = "g",
            OutstandingPurchaseQuantity = 700,
            State = GroceryItemState.Outstanding,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        item.Contributions.Add(new GroceryContribution
        {
            GroceryList = list,
            GroceryItem = item,
            RecipeAddition = addition,
            OwnerUserId = owner.Id,
            Origin = GroceryContributionOrigin.Recipe,
            OriginalNameText = "Oats",
            OriginalAmountText = "500",
            OriginalUnitText = "g",
            InterpretedAmount = 500,
            Dimension = QuantityDimension.Mass,
            SourceRecipeId = recipeId,
            SourceRecipeNameSnapshot = recipe.Name,
            State = GroceryContributionState.Outstanding,
            CreatedAtUtc = now
        });
        item.Contributions.Add(new GroceryContribution
        {
            GroceryList = list,
            GroceryItem = item,
            OwnerUserId = owner.Id,
            Origin = GroceryContributionOrigin.Manual,
            OriginalNameText = "Oats",
            OriginalAmountText = "200",
            OriginalUnitText = "g",
            Dimension = QuantityDimension.Mass,
            ManualPurchaseIncrement = 200,
            State = GroceryContributionState.Outstanding,
            CreatedAtUtc = now
        });
        db.AddRange(recipe, list, addition, item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
        _originalPurchaseQuantity = item.OutstandingPurchaseQuantity;
    }

    [When("I try to reduce the grocery line below its Recipe demand")]
    public async Task ReduceBelowRecipeDemandAsync()
    {
        using var listResponse = await context.Client.GetAsync("/api/grocery-list");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        context.Response?.Dispose();
        context.Response = await context.Client.PutAsJsonAsync(
            $"/api/grocery-list/items/{_itemId}",
            new
            {
                commandId = Guid.CreateVersion7(),
                expectedListVersion = list.RootElement.GetProperty("version").GetUInt32(),
                outstandingPurchaseQuantity = 400,
                purchaseUnit = "g",
                note = (string?)null,
                departmentUpdate = (object?)null,
                adHocStoreId = (Guid?)null,
                adHocDepartmentId = (Guid?)null
            });
    }

    [Then("the grocery API rejects the reduction and preserves the outstanding quantity")]
    public async Task AssertRecipeDemandFloorAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var listResponse = await context.Client.GetAsync("/api/grocery-list");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        list.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == _itemId)
            .GetProperty("outstandingPurchaseQuantity").GetDecimal()
            .Should().Be(_originalPurchaseQuantity);
    }
}
