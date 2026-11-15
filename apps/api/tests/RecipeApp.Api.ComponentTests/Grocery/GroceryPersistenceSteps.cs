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
public sealed class GroceryPersistenceSteps(ApiAuthContext context)
{
    private Guid _ownerId;
    private Guid _otherOwnerId;
    private Guid _otherIngredientId;
    private string _connectionString = string.Empty;
    private GroceryList? _firstCreatedList;
    private int _rejectedCrossOwnerWrites;

    [Given("an owner without a grocery list")]
    public async Task SeedOwnerWithoutListAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _connectionString = db.Database.GetConnectionString()!;
        var owner = RecipePersistenceTestData.CreateUser("grocery-first-owner");
        _ownerId = owner.Id;
        db.Users.Add(owner);
        await db.SaveChangesAsync();
    }

    [When("the owner reads the list without creating it")]
    public async Task ReadWithoutCreatingAsync()
    {
        await using var db = OpenDatabase();

        (await GroceryListPersistence.FindForOwnerAsync(db, _ownerId)).Should().BeNull();
        (await db.GroceryLists.CountAsync(list => list.OwnerUserId == _ownerId)).Should().Be(0);
    }

    [Then("the read is empty and no list is persisted")]
    public async Task AssertReadDidNotCreateListAsync()
    {
        await using var db = OpenDatabase();

        (await db.GroceryLists.CountAsync(list => list.OwnerUserId == _ownerId)).Should().Be(0);
    }

    [When("the owner issues concurrent first-list mutations")]
    public async Task CreateFirstListsConcurrentlyAsync()
    {
        var results = await Task.WhenAll(CreateOrGetListAsync(), CreateOrGetListAsync());

        results.Select(list => list.Id).Distinct().Should().ContainSingle();
        _firstCreatedList = results[0];
    }

    [Then("both mutations use the same persistent grocery list")]
    public async Task AssertOnePersistentListAsync()
    {
        _firstCreatedList.Should().NotBeNull();
        await using var db = OpenDatabase();

        var lists = await db.GroceryLists
            .Where(list => list.OwnerUserId == _ownerId)
            .ToArrayAsync();
        lists.Should().ContainSingle().Which.Id.Should().Be(_firstCreatedList!.Id);
    }

    [Given("two owners and a grocery list for the first owner")]
    public async Task SeedTwoOwnersAndListAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _connectionString = db.Database.GetConnectionString()!;
        var owner = RecipePersistenceTestData.CreateUser("grocery-owner");
        var otherOwner = RecipePersistenceTestData.CreateUser("grocery-other-owner");
        _ownerId = owner.Id;
        _otherOwnerId = otherOwner.Id;
        _otherIngredientId = Guid.CreateVersion7();
        db.Users.AddRange(owner, otherOwner);
        db.Ingredients.Add(new Ingredient
        {
            Id = _otherIngredientId,
            OwnerUserId = _otherOwnerId,
            Name = "Synthetic basil",
            NormalizedName = "synthetic basil",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.GroceryLists.Add(new GroceryList
        {
            OwnerUserId = _ownerId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [When("the first owner's grocery item references the second owner's Ingredient")]
    public async Task AddCrossOwnerIngredientReferenceAsync()
    {
        await using var db = OpenDatabase();
        var listId = await db.GroceryLists
            .Where(list => list.OwnerUserId == _ownerId)
            .Select(list => list.Id)
            .SingleAsync();
        db.GroceryItems.Add(new GroceryItem
        {
            GroceryListId = listId,
            OwnerUserId = _ownerId,
            IngredientId = _otherIngredientId,
            NameSnapshot = "Synthetic basil",
            NormalizedName = "synthetic basil",
            ConsolidationKey = GroceryConsolidationKey.ForIngredient(
                _otherIngredientId,
                QuantityDimension.Mass,
                null),
            Dimension = QuantityDimension.Mass,
            PurchaseUnit = "g",
            OutstandingPurchaseQuantity = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ForeignKeyViolation,
            ConstraintName: "FK_GroceryItems_Ingredients_IngredientId_OwnerUserId"
        })
        {
            db.ChangeTracker.Clear();
            _rejectedCrossOwnerWrites++;
        }
    }

    [Then("the cross-owner grocery link is rejected")]
    public async Task AssertCrossOwnerLinkWasRejectedAsync()
    {
        _rejectedCrossOwnerWrites.Should().Be(1);
        await using var db = OpenDatabase();

        (await db.GroceryItems.CountAsync()).Should().Be(0);
    }

    private async Task<GroceryList> CreateOrGetListAsync()
    {
        await using var db = OpenDatabase();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var list = await GroceryListPersistence.GetOrCreateForMutationAsync(
            db,
            _ownerId,
            DateTime.UtcNow);
        await transaction.CommitAsync();
        return list;
    }

    private AppDbContext OpenDatabase() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options);
}
