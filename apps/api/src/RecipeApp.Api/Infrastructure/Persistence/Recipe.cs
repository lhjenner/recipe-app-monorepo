namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class Recipe
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? LegacyId { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public string? SourceText { get; set; }
    public string? SourceUrl { get; set; }
    public string? ServingsText { get; set; }
    public string? PrepTimeText { get; set; }
    public string? CookTimeText { get; set; }
    public DateTime? DateAddedAtUtc { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<RecipeStep> Steps { get; } = new List<RecipeStep>();
    public ICollection<RecipeTag> Tags { get; } = new List<RecipeTag>();
    public RecipeImage? Image { get; set; }
    public RecipeImportProvenance? ImportProvenance { get; set; }
}
