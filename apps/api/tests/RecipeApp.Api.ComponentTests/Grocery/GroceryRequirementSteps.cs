using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryRequirementSteps(ApiAuthContext context)
{
    [Given("the grocery item has historical completed and cancelled requirements")]
    public async Task SeedHistoricalRequirementsAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await GetOwnedItemAsync(db);
        foreach (var state in new[] { GroceryContributionState.Completed, GroceryContributionState.Cancelled })
        {
            db.GroceryContributions.Add(new GroceryContribution
            {
                GroceryListId = item.GroceryListId,
                GroceryItemId = item.Id,
                OwnerUserId = item.OwnerUserId,
                Origin = GroceryContributionOrigin.Manual,
                OriginalAmountText = "7",
                OriginalUnitText = "onion",
                InterpretedAmount = 7,
                Dimension = QuantityDimension.Count,
                CountLabel = "onion",
                State = state,
                CreatedAtUtc = item.CreatedAtUtc.AddSeconds(1)
            });
        }
        await db.SaveChangesAsync();
    }

    [Given("the grocery item and its requirement are completed")]
    public async Task CompleteRequirementAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await GetOwnedItemAsync(db);
        item.State = GroceryItemState.Completed;
        item.OutstandingPurchaseQuantity = 0;
        var contribution = await db.GroceryContributions.SingleAsync(value => value.GroceryItemId == item.Id);
        contribution.State = GroceryContributionState.Completed;
        db.GroceryPurchaseEvents.Add(new GroceryPurchaseEvent
        {
            GroceryListId = item.GroceryListId,
            GroceryItemId = item.Id,
            OwnerUserId = item.OwnerUserId,
            Kind = GroceryPurchaseEventKind.Purchase,
            Quantity = 2,
            PurchaseUnit = item.PurchaseUnit,
            OccurredAtUtc = context.Time.GetUtcNow().UtcDateTime,
            ItemNameSnapshot = item.NameSnapshot,
            Origin = GroceryPurchaseOrigin.Manual
        });
        await db.SaveChangesAsync();
    }

    [When("I request the grocery list including completed items")]
    public async Task RequestCompletedItemsAsync() =>
        context.Response = await context.Client.GetAsync("/api/grocery-list?includeCompleted=true");

    [Then("the grocery item retains all {int} source contributions")]
    public async Task AssertSourceEvidenceAsync(int count)
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        var contributions = document.RootElement.GetProperty("items")[0].GetProperty("contributions");
        contributions.GetArrayLength().Should().Be(count);
        contributions.EnumerateArray().Select(value => value.GetProperty("state").GetString())
            .Should().BeEquivalentTo("outstanding", "completed", "cancelled");
    }

    private async Task<GroceryItem> GetOwnedItemAsync(AppDbContext db) =>
        await db.GroceryItems.SingleAsync(item => item.OwnerUserId ==
            db.Users.Where(user => user.Email == context.Email.Trim().ToLowerInvariant())
                .Select(user => user.Id).Single());
}
