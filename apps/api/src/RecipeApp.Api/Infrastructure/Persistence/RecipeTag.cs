namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class RecipeTag
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RecipeId { get; set; }
    public int Position { get; set; }
    public required string Text { get; set; }
}
