namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class SupermarketChain
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public ICollection<Store> Stores { get; } = new List<Store>();
}
