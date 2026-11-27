using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class GroceryPurchaseEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroceryListId { get; set; }
    public Guid GroceryItemId { get; set; }
    public Guid OwnerUserId { get; set; }
    public GroceryPurchaseEventKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public required string PurchaseUnit { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public required string ItemNameSnapshot { get; set; }
    public string? NoteSnapshot { get; set; }
    public Guid? StoreIdSnapshot { get; set; }
    public string? StoreNameSnapshot { get; set; }
    public GroceryPurchaseOrigin Origin { get; set; }
    public Guid? ReversesEventId { get; set; }
    public string AffectedContributionIdsJson { get; set; } = "[]";
    public GroceryList? GroceryList { get; set; }
    public GroceryItem? GroceryItem { get; set; }
    public GroceryPurchaseEvent? ReversesEvent { get; set; }
}
