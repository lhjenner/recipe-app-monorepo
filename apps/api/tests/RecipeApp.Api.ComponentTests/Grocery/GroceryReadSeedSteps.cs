using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Auth;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.ComponentTests.Recipes;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryReadSeedSteps(ApiAuthContext context)
{
    [Given("another owner has grocery items and a purchase event")]
    public async Task SeedOtherOwnerGroceryDataAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = RecipePersistenceTestData.CreateUser("grocery-read-other-owner");
        var now = DateTime.UtcNow;
        var list = new GroceryList { OwnerUserId = owner.Id, CreatedAtUtc = now, UpdatedAtUtc = now };
        var item = new GroceryItem
        {
            GroceryList = list,
            OwnerUserId = owner.Id,
            NameSnapshot = "Synthetic pears",
            NormalizedName = "synthetic pears",
            ConsolidationKey = "ad-hoc:synthetic-pears",
            PurchaseUnit = "item",
            OutstandingPurchaseQuantity = 1,
            State = GroceryItemState.Completed,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.Users.Add(owner);
        db.GroceryLists.Add(list);
        db.GroceryItems.Add(item);
        db.GroceryPurchaseEvents.Add(new GroceryPurchaseEvent
        {
            GroceryList = list,
            GroceryItem = item,
            OwnerUserId = owner.Id,
            Kind = GroceryPurchaseEventKind.Purchase,
            Quantity = 1,
            PurchaseUnit = "item",
            OccurredAtUtc = now,
            ItemNameSnapshot = item.NameSnapshot,
            Origin = GroceryPurchaseOrigin.Manual
        });
        await db.SaveChangesAsync();
    }

    [Given("the owner has a grocery item with a source requirement and current store mapping")]
    public async Task SeedOwnedGroceryItemAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GetCurrentOwnerAsync(db);
        var now = DateTime.UtcNow;
        var ingredientId = Guid.CreateVersion7();
        var department = new Department
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "produce",
            NormalizedName = "produce"
        };
        var ingredient = new Ingredient
        {
            Id = ingredientId,
            OwnerUserId = owner.Id,
            Name = "current onion name",
            NormalizedName = "current onion name",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ingredient.StoreMappings.Add(new IngredientStoreMapping
        {
            IngredientId = ingredientId,
            StoreId = Guid.Parse("67af7315-c4e5-4f9e-89b3-7c3a71ba11da"),
            DepartmentId = department.Id
        });
        var list = new GroceryList { OwnerUserId = owner.Id, CreatedAtUtc = now, UpdatedAtUtc = now };
        var item = new GroceryItem
        {
            GroceryList = list,
            OwnerUserId = owner.Id,
            Ingredient = ingredient,
            NameSnapshot = "old onion name",
            NormalizedName = "current onion name",
            ConsolidationKey = GroceryConsolidationKey.ForIngredient(
                ingredientId,
                QuantityDimension.Count,
                "onion"),
            Dimension = QuantityDimension.Count,
            CountLabel = "onion",
            PurchaseUnit = "onion",
            OutstandingPurchaseQuantity = 2,
            State = GroceryItemState.Outstanding,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var contribution = new GroceryContribution
        {
            GroceryList = list,
            GroceryItem = item,
            OwnerUserId = owner.Id,
            Origin = GroceryContributionOrigin.Manual,
            OriginalAmountText = "2",
            OriginalUnitText = "onion",
            InterpretedAmount = 2,
            Dimension = QuantityDimension.Count,
            CountLabel = "onion",
            State = GroceryContributionState.Outstanding,
            CreatedAtUtc = now
        };

        db.AddRange(department, ingredient, list, item, contribution);
        await db.SaveChangesAsync();
    }

    [Given("purchase events span the Auckland date boundary for {string}")]
    public async Task SeedAucklandBoundaryEventsAsync(string date)
    {
        var targetDate = DateOnly.Parse(date);
        var startUtc = targetDate.AddDays(-1).ToDateTime(new TimeOnly(11, 0), DateTimeKind.Utc);
        var endUtc = targetDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await GetCurrentOwnerAsync(db);
        var list = new GroceryList
        {
            OwnerUserId = owner.Id,
            CreatedAtUtc = startUtc,
            UpdatedAtUtc = startUtc
        };
        var item = new GroceryItem
        {
            GroceryList = list,
            OwnerUserId = owner.Id,
            NameSnapshot = "Pears",
            NormalizedName = "pears",
            ConsolidationKey = "ad-hoc:pears",
            PurchaseUnit = "item",
            OutstandingPurchaseQuantity = 0,
            State = GroceryItemState.Completed,
            CreatedAtUtc = startUtc,
            UpdatedAtUtc = startUtc
        };
        var before = CreatePurchase(list, item, owner.Id, startUtc.AddMinutes(-1), "Before");
        var inRange = CreatePurchase(list, item, owner.Id, startUtc, "In range");
        var after = CreatePurchase(list, item, owner.Id, endUtc, "Next day");
        var reversal = CreateReversal(list, item, owner.Id, endUtc, inRange);

        db.GroceryLists.Add(list);
        db.GroceryItems.Add(item);
        db.GroceryPurchaseEvents.AddRange(before, inRange, after, reversal);
        await db.SaveChangesAsync();
    }

    private async Task<User> GetCurrentOwnerAsync(AppDbContext db) =>
        await db.Users.AsNoTracking().SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());

    private static GroceryPurchaseEvent CreatePurchase(
        GroceryList list,
        GroceryItem item,
        Guid ownerId,
        DateTime occurredAtUtc,
        string name) =>
        new()
        {
            GroceryList = list,
            GroceryItem = item,
            OwnerUserId = ownerId,
            Kind = GroceryPurchaseEventKind.Purchase,
            Quantity = 1,
            PurchaseUnit = "item",
            OccurredAtUtc = occurredAtUtc,
            ItemNameSnapshot = name,
            Origin = GroceryPurchaseOrigin.Manual
        };

    private static GroceryPurchaseEvent CreateReversal(
        GroceryList list,
        GroceryItem item,
        Guid ownerId,
        DateTime occurredAtUtc,
        GroceryPurchaseEvent purchase) =>
        new()
        {
            GroceryList = list,
            GroceryItem = item,
            OwnerUserId = ownerId,
            Kind = GroceryPurchaseEventKind.Reversal,
            Quantity = 1,
            PurchaseUnit = "item",
            OccurredAtUtc = occurredAtUtc,
            ItemNameSnapshot = purchase.ItemNameSnapshot,
            Origin = GroceryPurchaseOrigin.Manual,
            ReversesEvent = purchase
        };
}
