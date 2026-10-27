using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Recipes;

[Binding]
public sealed class RecipeIdentityPersistenceSteps(ApiAuthContext context)
{
    private Guid _ownerId;
    private Guid _otherOwnerId;
    private Guid _thirdOwnerId;
    private Guid _recipeId;
    private int _rejectedWrites;

    [Given("two recipe owners")]
    public async Task SeedTwoOwnersAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = RecipePersistenceTestData.CreateUser("recipe-owner");
        var otherOwner = RecipePersistenceTestData.CreateUser("recipe-other-owner");
        var thirdOwner = RecipePersistenceTestData.CreateUser("recipe-third-owner");
        _ownerId = owner.Id;
        _otherOwnerId = otherOwner.Id;
        _thirdOwnerId = thirdOwner.Id;
        db.Users.AddRange(owner, otherOwner, thirdOwner);
        await db.SaveChangesAsync();
    }

    [When("null, case-distinct, and cross-owner legacy identities are saved")]
    public async Task SaveLegacyIdentitiesAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mappedRecipe = RecipePersistenceTestData.CreateRecipe(_ownerId, "Source-A");
        _recipeId = mappedRecipe.Id;
        db.Recipes.AddRange(
            RecipePersistenceTestData.CreateRecipe(_ownerId, null),
            RecipePersistenceTestData.CreateRecipe(_ownerId, null),
            mappedRecipe,
            RecipePersistenceTestData.CreateRecipe(_ownerId, "source-a"),
            RecipePersistenceTestData.CreateRecipe(_otherOwnerId, "Source-A"));
        await db.SaveChangesAsync();

        db.Recipes.Add(RecipePersistenceTestData.CreateRecipe(_ownerId, "Source-A"));
        await AssertRejectedAsync(db);
        db.Recipes.Add(RecipePersistenceTestData.CreateRecipe(_ownerId, "  "));
        await AssertRejectedAsync(db);
    }

    [Then("those identities coexist and exact duplicates and blank identities are rejected")]
    public async Task AssertIdentityRulesAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Recipes.CountAsync()).Should().Be(5);
        (await db.Recipes.CountAsync(row => row.LegacyId == null)).Should().Be(2);
        _rejectedWrites.Should().Be(2);
    }

    [When("a persisted recipe legacy identity and owner are changed")]
    public async Task ChangePersistedIdentityAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipe = await db.Recipes.SingleAsync(row => row.Id == _recipeId);
        recipe.LegacyId = "Source-B";
        await AssertRejectedAsync(db);

        var act = () => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Recipes\" SET \"OwnerUserId\" = {_thirdOwnerId} WHERE \"Id\" = {_recipeId}");
        await act.Should().ThrowAsync<PostgresException>();
        ++_rejectedWrites;
    }

    [Then("the database rejects identity reassignment")]
    public void AssertIdentityReassignmentRejected() =>
        _rejectedWrites.Should().Be(4);

    private async Task AssertRejectedAsync(AppDbContext db)
    {
        await RecipePersistenceTestData.AssertWriteRejectedAsync(db);
        ++_rejectedWrites;
    }
}
