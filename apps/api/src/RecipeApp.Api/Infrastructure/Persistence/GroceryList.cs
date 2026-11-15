namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class GroceryList
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<GroceryItem> Items { get; } = new List<GroceryItem>();
    public ICollection<GroceryRecipeAddition> RecipeAdditions { get; } = new List<GroceryRecipeAddition>();
    public ICollection<GroceryPurchaseEvent> PurchaseEvents { get; } = new List<GroceryPurchaseEvent>();
    public ICollection<GroceryCommandReceipt> CommandReceipts { get; } = new List<GroceryCommandReceipt>();
}
