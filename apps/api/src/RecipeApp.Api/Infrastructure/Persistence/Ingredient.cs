namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class Ingredient
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    public bool ExcludeFromGroceryList { get; set; }
    public NutritionPer100g? Nutrition { get; set; }
    public string? NutritionSource { get; set; }
    public DateTime? NutritionUpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<IngredientStoreMapping> StoreMappings { get; } = new List<IngredientStoreMapping>();
    public ICollection<ProductLink> ProductLinks { get; } = new List<ProductLink>();
}

public sealed class NutritionPer100g
{
    public decimal? EnergyKcal { get; set; }
    public decimal? ProteinG { get; set; }
    public decimal? CarbohydrateG { get; set; }
    public decimal? SugarG { get; set; }
    public decimal? FatG { get; set; }
    public decimal? SaturatedFatG { get; set; }
    public decimal? FibreG { get; set; }
    public decimal? SodiumMg { get; set; }
}
