using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class GroceryItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroceryListId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid? IngredientId { get; set; }
    public required string NameSnapshot { get; set; }
    public required string NormalizedName { get; set; }
    public string? Note { get; set; }
    public Guid? StoreId { get; set; }
    public Guid? DepartmentId { get; set; }
    public QuantityDimension? Dimension { get; set; }
    public string? CountLabel { get; set; }
    public required string ConsolidationKey { get; set; }
    public required string PurchaseUnit { get; set; }
    public decimal OutstandingPurchaseQuantity { get; set; }
    public decimal RestoredPurchaseQuantity { get; set; }
    public GroceryItemState State { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public GroceryList? GroceryList { get; set; }
    public Ingredient? Ingredient { get; set; }
    public Department? Department { get; set; }
    public ICollection<GroceryContribution> Contributions { get; } = new List<GroceryContribution>();
    public ICollection<GroceryPurchaseEvent> PurchaseEvents { get; } = new List<GroceryPurchaseEvent>();
}
