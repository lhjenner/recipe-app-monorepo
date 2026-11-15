using RecipeApp.Api.Grocery;

namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class GroceryContribution
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid GroceryListId { get; set; }
    public Guid GroceryItemId { get; set; }
    public Guid OwnerUserId { get; set; }
    public GroceryContributionOrigin Origin { get; set; }
    public Guid? RecipeAdditionId { get; set; }
    public string? OriginalNameText { get; set; }
    public string? OriginalAmountText { get; set; }
    public string? OriginalUnitText { get; set; }
    public string? OriginalNotesText { get; set; }
    public decimal? InterpretedAmount { get; set; }
    public QuantityDimension? Dimension { get; set; }
    public string? CountLabel { get; set; }
    public string? UnresolvedReason { get; set; }
    public Guid? SourceRecipeId { get; set; }
    public Guid? SourceRecipeStepId { get; set; }
    public Guid? SourceRecipeStepItemId { get; set; }
    public string? SourceRecipeNameSnapshot { get; set; }
    public decimal? ManualPurchaseIncrement { get; set; }
    public GroceryContributionState State { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public GroceryList? GroceryList { get; set; }
    public GroceryItem? GroceryItem { get; set; }
    public GroceryRecipeAddition? RecipeAddition { get; set; }
    public Recipe? SourceRecipe { get; set; }
}
