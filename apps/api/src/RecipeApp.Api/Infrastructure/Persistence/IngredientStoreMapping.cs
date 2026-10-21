namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class IngredientStoreMapping
{
    public Guid IngredientId { get; set; }
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsNotStocked { get; set; }
}
