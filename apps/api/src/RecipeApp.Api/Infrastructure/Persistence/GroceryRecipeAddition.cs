namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class GroceryRecipeAddition
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroceryListId { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid RecipeId { get; set; }
    public required string RecipeNameSnapshot { get; set; }
    public uint RecipeVersion { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public GroceryList? GroceryList { get; set; }
    public ICollection<GroceryContribution> Contributions { get; } = new List<GroceryContribution>();
}
