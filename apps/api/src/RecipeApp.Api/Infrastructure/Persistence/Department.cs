namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class Department
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
}
