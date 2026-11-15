using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.UnitTests.Grocery;

public sealed class GroceryPersistenceModelTests
{
    private readonly AppDbContext _db = new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options);

    [Fact]
    public void List_is_unique_per_owner_and_uses_database_row_version()
    {
        var list = _db.Model.FindEntityType(typeof(GroceryList))!;
        var ownerIndex = list.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_GroceryLists_OwnerUserId");

        ownerIndex.IsUnique.Should().BeTrue();
        list.FindProperty(nameof(GroceryList.Version))!.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void Child_foreign_keys_bind_the_owner_and_list_identity()
    {
        var item = _db.Model.FindEntityType(typeof(GroceryItem))!;
        var ingredientReference = item.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(Ingredient));
        var listReferences = item.GetForeignKeys()
            .Where(key => key.PrincipalEntityType.ClrType == typeof(GroceryList))
            .ToArray();

        ingredientReference.Properties.Select(property => property.Name)
            .Should().Equal(nameof(GroceryItem.IngredientId), nameof(GroceryItem.OwnerUserId));
        listReferences.Should().ContainSingle();
        listReferences[0].Properties.Select(property => property.Name)
            .Should().Equal(nameof(GroceryItem.GroceryListId), nameof(GroceryItem.OwnerUserId));
    }

    [Fact]
    public void Active_consolidation_and_reversal_receipts_have_unique_database_keys()
    {
        var item = _db.Model.FindEntityType(typeof(GroceryItem))!;
        var activeKey = item.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_GroceryItems_ActiveConsolidationKey");
        var purchaseEvent = _db.Model.FindEntityType(typeof(GroceryPurchaseEvent))!;
        var reversalKey = purchaseEvent.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_GroceryPurchaseEvents_OneReversalPerEvent");
        var receipt = _db.Model.FindEntityType(typeof(GroceryCommandReceipt))!;
        var commandKey = receipt.GetIndexes()
            .Single(index => index.GetDatabaseName() == "IX_GroceryCommandReceipts_Owner_List_Command");

        activeKey.IsUnique.Should().BeTrue();
        activeKey.GetFilter().Should().Be("\"State\" = 0");
        reversalKey.IsUnique.Should().BeTrue();
        commandKey.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Stored_grocery_amounts_use_the_documented_decimal_contract()
    {
        foreach (var type in new[]
                 {
                     typeof(GroceryItem),
                     typeof(GroceryContribution),
                     typeof(GroceryPurchaseEvent)
                 })
        {
            var entity = _db.Model.FindEntityType(type)!;
            var amountProperties = entity.GetProperties()
                .Where(property => property.ClrType == typeof(decimal)
                    || property.ClrType == typeof(decimal?))
                .ToArray();

            amountProperties.Should().NotBeEmpty();
            amountProperties.Should().OnlyContain(property =>
                property.GetPrecision() == 28 && property.GetScale() == 9);
        }
    }

    [Fact]
    public void Recipe_name_snapshots_preserve_unrestricted_source_text()
    {
        var addition = _db.Model.FindEntityType(typeof(GroceryRecipeAddition))!;
        var recipeName = addition.FindProperty(nameof(GroceryRecipeAddition.RecipeNameSnapshot))!;

        recipeName.IsNullable.Should().BeFalse();
        recipeName.GetMaxLength().Should().BeNull();
        recipeName.GetColumnType().Should().Be("text");
    }
}
