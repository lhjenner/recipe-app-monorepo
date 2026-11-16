using System.Net;
using System.Net.Http.Json;
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
public sealed class GroceryCommandReviewSteps(ApiAuthContext context)
{
    private Guid _secondItemId;
    private Guid _ownerId;

    [When("I replay a grocery {string} command against another item")]
    public async Task ReplayAgainstAnotherItemAsync(string operation)
    {
        using var created = await context.Client.PostAsJsonAsync("/api/grocery-list/items",
            new GroceryManualEntryCommand(Guid.NewGuid(), 0,
                [new("Apples", null, 1, "item"), new("Pears", null, 1, "item")], []));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        using var list = await ReadListAsync();
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var firstId = items[0].GetProperty("id").GetGuid();
        _secondItemId = items[1].GetProperty("id").GetGuid();
        var commandId = Guid.NewGuid();
        var version = list.RootElement.GetProperty("version").GetUInt32();
        var update = new GroceryItemUpdateRequest(commandId, version, 2, "item", "Updated", null, null, null);
        using var first = await SendAsync(firstId);
        first.StatusCode.Should().Be(operation == "update" ? HttpStatusCode.OK : HttpStatusCode.NoContent);
        using var replay = await SendAsync(firstId);
        replay.StatusCode.Should().Be(first.StatusCode);
        context.Response?.Dispose();
        context.Response = await SendAsync(_secondItemId);

        Task<HttpResponseMessage> SendAsync(Guid id)
        {
            if (operation == "update")
            {
                return context.Client.PutAsJsonAsync($"/api/grocery-list/items/{id}", update);
            }

            return CancelAsync(id, commandId, version);
        }
    }

    [Then("the changed-target grocery command is rejected without changing the second item")]
    public async Task AssertChangedTargetAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var list = await ReadListAsync();
        var item = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("id").GetGuid() == _secondItemId);
        item.GetProperty("outstandingPurchaseQuantity").GetDecimal().Should().Be(1);
        item.GetProperty("note").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [When("I submit a grocery batch with a valid row and a {string} Ingredient")]
    public async Task SubmitInvalidIngredientAsync(string eligibility)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _ownerId = (await db.Users.SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant())).Id;
        var ingredientId = Guid.NewGuid();
        if (eligibility != "missing")
        {
            var ingredientOwnerId = _ownerId;
            if (eligibility == "other-owner")
            {
                var other = new RecipeApp.Api.Auth.User
                {
                    Id = Guid.NewGuid(),
                    Email = $"{Guid.NewGuid():N}@example.test",
                    PasswordHash = "not-used",
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.Users.Add(other);
                ingredientOwnerId = other.Id;
            }

            db.Ingredients.Add(new Ingredient
            {
                Id = ingredientId,
                OwnerUserId = ingredientOwnerId,
                Name = "Oats",
                NormalizedName = "oats",
                ExcludeFromGroceryList = eligibility == "excluded",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        context.Response?.Dispose();
        context.Response = await context.Client.PostAsJsonAsync("/api/grocery-list/items",
            new GroceryManualEntryCommand(Guid.NewGuid(), 0,
                [new("Apples", null, 1, "item"), new("Oats", null, 1, "item", ingredientId)], []));
    }

    [Then("the invalid Ingredient batch returns {int} without creating a list or receipt")]
    public async Task AssertRejectedBatchAsync(int status)
    {
        ((int)context.Response!.StatusCode).Should().Be(status);
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.GroceryLists.AnyAsync(value => value.OwnerUserId == _ownerId)).Should().BeFalse();
        (await db.GroceryItems.AnyAsync(value => value.OwnerUserId == _ownerId)).Should().BeFalse();
        (await db.GroceryCommandReceipts.AnyAsync(value => value.OwnerUserId == _ownerId)).Should().BeFalse();
    }

    private async Task<HttpResponseMessage> CancelAsync(Guid id, Guid commandId, uint version)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/grocery-list/items/{id}");
        request.Headers.Add("X-Command-Id", commandId.ToString());
        request.Headers.Add("X-Expected-List-Version", version.ToString());
        return await context.Client.SendAsync(request);
    }

    private async Task<JsonDocument> ReadListAsync()
    {
        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
