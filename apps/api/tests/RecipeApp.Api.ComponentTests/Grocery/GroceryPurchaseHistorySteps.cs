using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.ComponentTests.Recipes;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryPurchaseHistorySteps(ApiAuthContext context)
{
    private Guid _ownerId;
    private Guid _listId;
    private Guid _itemId;
    private Guid _purchaseEventId;
    private int _duplicateReversalsRejected;
    private int _eventMutationsRejected;
    private string _connectionString = string.Empty;

    [Given("a purchase event for an owner exists")]
    public async Task SeedPurchaseEventAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _connectionString = db.Database.GetConnectionString()!;
        var owner = RecipePersistenceTestData.CreateUser("grocery-history-owner");
        _ownerId = owner.Id;
        var now = DateTime.UtcNow;
        var list = new GroceryList
        {
            OwnerUserId = owner.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var item = new GroceryItem
        {
            GroceryListId = list.Id,
            OwnerUserId = owner.Id,
            NameSnapshot = "Synthetic tomatoes",
            NormalizedName = "synthetic tomatoes",
            Dimension = QuantityDimension.Count,
            CountLabel = "tin",
            ConsolidationKey = GroceryConsolidationKey.ForName(
                "Synthetic tomatoes",
                QuantityDimension.Count,
                "tin"),
            PurchaseUnit = "tin",
            OutstandingPurchaseQuantity = 0,
            State = GroceryItemState.Completed,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _listId = list.Id;
        _itemId = item.Id;
        _purchaseEventId = Guid.CreateVersion7();
        db.Users.Add(owner);
        db.GroceryLists.Add(list);
        db.GroceryItems.Add(item);
        db.GroceryPurchaseEvents.Add(new GroceryPurchaseEvent
        {
            Id = _purchaseEventId,
            GroceryListId = list.Id,
            GroceryItemId = item.Id,
            OwnerUserId = owner.Id,
            Kind = GroceryPurchaseEventKind.Purchase,
            Quantity = 3,
            PurchaseUnit = "tin",
            OccurredAtUtc = now.AddDays(-1),
            ItemNameSnapshot = item.NameSnapshot,
            Origin = GroceryPurchaseOrigin.Manual
        });
        await db.SaveChangesAsync();
    }

    [When("an exact reversal is recorded")]
    public async Task RecordExactReversalAsync()
    {
        await using var db = OpenDatabase();
        db.GroceryPurchaseEvents.Add(CreateReversal());
        await db.SaveChangesAsync();
    }

    [Then("the original purchase and reversal are both retained")]
    public async Task AssertPurchaseAndReversalRemainAsync()
    {
        await using var db = OpenDatabase();

        (await db.GroceryPurchaseEvents.CountAsync()).Should().Be(2);
        (await db.GroceryPurchaseEvents.CountAsync(item => item.Id == _purchaseEventId)).Should().Be(1);
        (await db.GroceryPurchaseEvents.CountAsync(item => item.ReversesEventId == _purchaseEventId)).Should().Be(1);
    }

    [When("the purchase is reversed a second time")]
    public async Task AttemptDuplicateReversalAsync()
    {
        await using var db = OpenDatabase();
        db.GroceryPurchaseEvents.Add(CreateReversal());

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_GroceryPurchaseEvents_OneReversalPerEvent"
        })
        {
            _duplicateReversalsRejected++;
        }
    }

    [Then("the duplicate reversal is rejected")]
    public void AssertDuplicateReversalRejected() =>
        _duplicateReversalsRejected.Should().Be(1);

    [When("the original purchase event is edited")]
    public async Task EditOriginalPurchaseEventAsync()
    {
        await using var db = OpenDatabase();
        var purchaseEvent = await db.GroceryPurchaseEvents
            .SingleAsync(item => item.Id == _purchaseEventId);
        purchaseEvent.ItemNameSnapshot = "Changed synthetic tomatoes";

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: "23000"
        })
        {
            _eventMutationsRejected++;
        }
    }

    [Then("the purchase event mutation is rejected")]
    public async Task AssertEventMutationRejectedAsync()
    {
        _eventMutationsRejected.Should().Be(1);
        await using var db = OpenDatabase();

        var purchaseEvent = await db.GroceryPurchaseEvents
            .SingleAsync(item => item.Id == _purchaseEventId);
        purchaseEvent.ItemNameSnapshot.Should().Be("Synthetic tomatoes");
    }

    private GroceryPurchaseEvent CreateReversal() => new()
    {
        GroceryListId = _listId,
        GroceryItemId = _itemId,
        OwnerUserId = _ownerId,
        Kind = GroceryPurchaseEventKind.Reversal,
        Quantity = 3,
        PurchaseUnit = "tin",
        OccurredAtUtc = DateTime.UtcNow,
        ItemNameSnapshot = "Synthetic tomatoes",
        Origin = GroceryPurchaseOrigin.Manual,
        ReversesEventId = _purchaseEventId
    };

    private AppDbContext OpenDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options);
}
