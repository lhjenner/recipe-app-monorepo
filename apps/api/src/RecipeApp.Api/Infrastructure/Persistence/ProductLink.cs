namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class ProductLink
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public Guid ChainId { get; set; }
    public SupermarketChain? Chain { get; set; }
    public required string Url { get; set; }
    public string? VariantLabel { get; set; }
    public string? Note { get; set; }
    public bool SupportsBothPricingMethods { get; set; }
    public bool IsPreferred { get; set; }
    public int Position { get; set; }
}
