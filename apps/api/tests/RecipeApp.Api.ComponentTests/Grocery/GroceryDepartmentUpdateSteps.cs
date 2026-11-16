using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using RecipeApp.Api.Auth;
using RecipeApp.Api.ComponentTests.Auth;
using RecipeApp.Api.Grocery;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.ComponentTests.Grocery;

[Binding]
public sealed class GroceryDepartmentUpdateSteps(ApiAuthContext context)
{
    private static readonly Guid StoreId = Guid.Parse("67af7315-c4e5-4f9e-89b3-7c3a71ba11da");
    private Guid _itemId;
    private Guid _ingredientId;
    private Guid _departmentId;
    private string? _beforeInvalidEdit;

    [Given("the owner has a linked grocery item and a new department")]
    public async Task SeedLinkedGroceryItemAsync()
    {
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = await db.Users.SingleAsync(user =>
            user.Email == context.Email.Trim().ToLowerInvariant());
        var now = DateTime.UtcNow;
        var currentDepartment = new Department
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "Current department",
            NormalizedName = "current department"
        };
        var newDepartment = new Department
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "New department",
            NormalizedName = "new department"
        };
        var ingredient = new Ingredient
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = owner.Id,
            Name = "Oats",
            NormalizedName = "oats",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ingredient.StoreMappings.Add(new IngredientStoreMapping
        {
            IngredientId = ingredient.Id,
            StoreId = StoreId,
            DepartmentId = currentDepartment.Id
        });
        var list = new GroceryList
        {
            OwnerUserId = owner.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var item = new GroceryItem
        {
            GroceryList = list,
            OwnerUserId = owner.Id,
            Ingredient = ingredient,
            NameSnapshot = ingredient.Name,
            NormalizedName = ingredient.NormalizedName,
            ConsolidationKey = GroceryConsolidationKey.ForIngredient(
                ingredient.Id, QuantityDimension.Count, "piece"),
            Dimension = QuantityDimension.Count,
            CountLabel = "piece",
            PurchaseUnit = "piece",
            OutstandingPurchaseQuantity = 1,
            State = GroceryItemState.Outstanding,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        db.AddRange(currentDepartment, newDepartment, ingredient, list, item);
        await db.SaveChangesAsync();
        _itemId = item.Id;
        _ingredientId = ingredient.Id;
        _departmentId = newDepartment.Id;
    }

    [When("I update the linked Ingredient department for the seeded store")]
    public async Task UpdateDepartmentAsync()
    {
        using var listResponse = await context.Client.GetAsync("/api/grocery-list");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var item = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("id").GetGuid() == _itemId);
        var ingredientVersion = item.GetProperty("ingredientVersion").GetUInt32();
        context.Response?.Dispose();
        context.Response = await context.Client.PutAsJsonAsync(
            $"/api/grocery-list/items/{_itemId}",
            new
            {
                commandId = Guid.CreateVersion7(),
                expectedListVersion = list.RootElement.GetProperty("version").GetUInt32(),
                outstandingPurchaseQuantity = 1,
                purchaseUnit = "piece",
                note = (string?)null,
                departmentUpdate = new
                {
                    storeId = StoreId,
                    departmentId = _departmentId,
                    expectedIngredientVersion = ingredientVersion
                },
                adHocStoreId = (Guid?)null,
                adHocDepartmentId = (Guid?)null
            });
    }

    [Then("the grocery API updates the selected-store master department")]
    public async Task AssertDepartmentUpdatedAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.OK);
        using var response = await context.Client.GetAsync("/api/grocery-list");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("id").GetGuid() == _itemId);
        item.GetProperty("ingredientId").GetGuid().Should().Be(_ingredientId);
        item.GetProperty("storeMappings").EnumerateArray()
            .Single(mapping => mapping.GetProperty("storeId").GetGuid() == StoreId)
            .GetProperty("departmentId").GetGuid().Should().Be(_departmentId);
    }

    [When("I submit a linked department edit containing {string}")]
    public async Task SubmitInvalidEditAsync(string field)
    {
        using var response = await context.Client.GetAsync("/api/grocery-list");
        _beforeInvalidEdit = await response.Content.ReadAsStringAsync();
        using var list = JsonDocument.Parse(_beforeInvalidEdit);
        var item = list.RootElement.GetProperty("items").EnumerateArray()
            .Single(value => value.GetProperty("id").GetGuid() == _itemId);
        var departmentUpdate = new Dictionary<string, object?>
        {
            ["storeId"] = StoreId,
            ["departmentId"] = _departmentId,
            ["expectedIngredientVersion"] = item.GetProperty("ingredientVersion").GetUInt32()
        };
        var request = new Dictionary<string, object?>
        {
            ["commandId"] = Guid.NewGuid(),
            ["expectedListVersion"] = list.RootElement.GetProperty("version").GetUInt32(),
            ["outstandingPurchaseQuantity"] = 2,
            ["purchaseUnit"] = "piece",
            ["note"] = "Must not save",
            ["departmentUpdate"] = departmentUpdate
        };
        if (field == "nested storeMappings")
        {
            departmentUpdate["storeMappings"] = Array.Empty<object>();
        }
        else
        {
            request[field] = field switch
            {
                "name" => "Replaced",
                "storeMappings" => Array.Empty<object>(),
                "adHocStoreId" => StoreId,
                _ => _departmentId
            };
        }

        context.Response?.Dispose();
        context.Response = await context.Client.PutAsJsonAsync($"/api/grocery-list/items/{_itemId}", request);
    }

    [Then("the invalid department edit returns 400 without writes")]
    public async Task AssertInvalidEditAsync()
    {
        context.Response!.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var response = await context.Client.GetAsync("/api/grocery-list");
        (await response.Content.ReadAsStringAsync()).Should().Be(_beforeInvalidEdit);
        await using var scope = context.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listId = await db.GroceryItems.Where(value => value.Id == _itemId)
            .Select(value => value.GroceryListId).SingleAsync();
        (await db.GroceryCommandReceipts.AnyAsync(value => value.GroceryListId == listId)).Should().BeFalse();
    }
}
