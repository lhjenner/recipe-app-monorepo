namespace RecipeApp.Api.Infrastructure.Persistence;

public enum RecipeStepItemKind
{
    Ingredient,
    Heading,
    Note
}

public sealed class RecipeStepItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipeStepId { get; set; }
    public Guid OwnerUserId { get; set; }
    public int Position { get; set; }
    public RecipeStepItemKind Kind { get; set; }
    public Guid? IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public required string Text { get; set; }
    public string? QuantityText { get; set; }
    public string? UnitText { get; set; }
    public string? NotesText { get; set; }
}
