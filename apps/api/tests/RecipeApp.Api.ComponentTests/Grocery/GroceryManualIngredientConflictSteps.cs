using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryManualIngredientConflictSteps(ApiAuthContext context)
{
    [Given("the owner has an active Ingredient named {string}")]
    public async Task SeedActiveIngredientAsync(string name)
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());
        db.Ingredients.Add(new Ingredient
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = name,
            NormalizedName = name.Trim().ToLowerInvariant(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Then("the grocery API requires an existing Ingredient selection without partial writes")]
    public async Task AssertIngredientConflictAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await context.Response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("detail").GetString()
            .Should().Contain("Select it from the Ingredient lookup.");

        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        list.RootElement.GetProperty("id").ValueKind.Should().Be(JsonValueKind.Null);
        list.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }
}
