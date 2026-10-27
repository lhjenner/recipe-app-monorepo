namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class RecipeStep
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipeId { get; set; }
    public Guid OwnerUserId { get; set; }
    public int Position { get; set; }
    public required string Instructions { get; set; }
    public ICollection<RecipeStepItem> Items { get; } = new List<RecipeStepItem>();
}
