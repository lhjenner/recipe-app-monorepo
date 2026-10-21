namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class Store
{
    public Guid Id { get; set; }
    public Guid ChainId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
    public SupermarketChain? Chain { get; set; }
}
