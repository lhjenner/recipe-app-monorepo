using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.ComponentTests.Recipes;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryPurchaseUnitUndoSteps(ApiAuthContext context)
{
    private Guid _ownerId;
    private Guid _listId;
    private Guid _ingredientId;
    private Guid _packItemId;
    private Guid _purchaseEventId;
    private string _connectionString = string.Empty;
    private string _requirementKey = string.Empty;

    [Given("a completed pack purchase and an active grams line with the same requirement")]
    public async Task SeedDifferentPurchaseUnitsAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _connectionString = db.Database.GetConnectionString()!;
        var owner = RecipePersistenceTestData.CreateUser("grocery-unit-undo");
        _ownerId = owner.Id;
        _ingredientId = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        var ingredient = new Ingredient
        {
            Id = _ingredientId,
            OwnerUserId = _ownerId,
            Name = "Synthetic flour",
            NormalizedName = "synthetic flour",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var list = new GroceryList
        {
            OwnerUserId = _ownerId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _listId = list.Id;
        _requirementKey = GroceryConsolidationKey.ForIngredient(
            _ingredientId,
            QuantityDimension.Mass,
            null);
        var packItem = CreateItem(list.Id, "pack", 0, GroceryItemState.Completed, now);
        var gramsItem = CreateItem(list.Id, "g", 250, GroceryItemState.Outstanding, now);
        _packItemId = packItem.Id;
        _purchaseEventId = Guid.CreateVersion7();

        db.Users.Add(owner);
        db.Ingredients.Add(ingredient);
        db.GroceryLists.Add(list);
        db.GroceryItems.AddRange(packItem, gramsItem);
        db.GroceryPurchaseEvents.Add(new GroceryPurchaseEvent
        {
            Id = _purchaseEventId,
            GroceryListId = _listId,
            GroceryItemId = _packItemId,
            OwnerUserId = _ownerId,
            Kind = GroceryPurchaseEventKind.Purchase,
            Quantity = 1,
            PurchaseUnit = "pack",
            OccurredAtUtc = now.AddDays(-1),
            ItemNameSnapshot = ingredient.Name,
            Origin = GroceryPurchaseOrigin.Manual
        });
        await db.SaveChangesAsync();
    }

    [When("the pack purchase is undone")]
    public async Task UndoPackPurchaseAsync()
    {
        await using var db = OpenDatabase();
        var packItem = await db.GroceryItems.SingleAsync(item => item.Id == _packItemId);
        var purchaseEvent = await db.GroceryPurchaseEvents
            .SingleAsync(item => item.Id == _purchaseEventId);
        packItem.OutstandingPurchaseQuantity += purchaseEvent.Quantity;
        packItem.State = GroceryItemState.Outstanding;
        db.GroceryPurchaseEvents.Add(new GroceryPurchaseEvent
        {
            GroceryListId = _listId,
            GroceryItemId = _packItemId,
            OwnerUserId = _ownerId,
            Kind = GroceryPurchaseEventKind.Reversal,
            Quantity = purchaseEvent.Quantity,
            PurchaseUnit = purchaseEvent.PurchaseUnit,
            OccurredAtUtc = DateTime.UtcNow,
            ItemNameSnapshot = purchaseEvent.ItemNameSnapshot,
            Origin = GroceryPurchaseOrigin.Manual,
            ReversesEventId = _purchaseEventId
        });

        await db.SaveChangesAsync();
    }

    [Then("both purchase-unit lines remain outstanding and the reversal is retained")]
    public async Task AssertPurchaseUnitLinesAndReversalAsync()
    {
        await using var db = OpenDatabase();
        var activeItems = await db.GroceryItems
            .Where(item => item.GroceryListId == _listId
                && item.State == GroceryItemState.Outstanding)
            .ToArrayAsync();

        activeItems.Should().HaveCount(2);
        activeItems.Select(item => item.PurchaseUnit)
            .Should().BeEquivalentTo("g", "pack");
        activeItems.Select(item => item.ConsolidationKey)
            .Distinct()
            .Should().ContainSingle()
            .Which.Should().Be(_requirementKey);
        activeItems.Single(item => item.PurchaseUnit == "pack")
            .OutstandingPurchaseQuantity.Should().Be(1);

        var reversal = await db.GroceryPurchaseEvents
            .SingleAsync(item => item.ReversesEventId == _purchaseEventId);
        reversal.Kind.Should().Be(GroceryPurchaseEventKind.Reversal);
        reversal.GroceryItemId.Should().Be(_packItemId);
    }

    private GroceryItem CreateItem(
        Guid listId,
        string purchaseUnit,
        decimal outstandingQuantity,
        GroceryItemState state,
        DateTime now) =>
        new()
        {
            GroceryListId = listId,
            OwnerUserId = _ownerId,
            IngredientId = _ingredientId,
            NameSnapshot = "Synthetic flour",
            NormalizedName = "synthetic flour",
            Dimension = QuantityDimension.Mass,
            ConsolidationKey = _requirementKey,
            PurchaseUnit = purchaseUnit,
            OutstandingPurchaseQuantity = outstandingQuantity,
            State = state,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private AppDbContext OpenDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options);
}
