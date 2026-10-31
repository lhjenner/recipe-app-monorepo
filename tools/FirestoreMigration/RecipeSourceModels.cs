namespace FirestoreMigration;

public enum RecipeSourceItemKind
{
    Ingredient,
    Heading,
    Note
}

public sealed record RecipeSourceItem(int Position, RecipeSourceItemKind Kind, Guid? IngredientId,
    Guid SourceId, string Text, string? QuantityText, string? UnitText, string? NotesText,
    string SourceJson, string JsonPath);

public sealed record RecipeSourceStep(int Position, string Instructions, IReadOnlyList<RecipeSourceItem> Items);

public sealed record RecipeSourceBlankPlaceholder(string LegacyId, string JsonPath, int StepPosition,
    int ItemPosition, string SourceJson);

public sealed record RecipeSourceFieldDisposition(string LegacyId, string JsonPath, string Disposition);

public sealed record RecipeOwnerFieldCounts(int Present, int Missing);

public sealed record RecipeSourceRecord(string LegacyId, string Name, string? SourceText, string? SourceUrl,
    string? ServingsText, string? PrepTimeText, string? CookTimeText, IReadOnlyList<string> Tags,
    IReadOnlyList<RecipeSourceStep> Steps, bool IsArchived, DateTime? DateAddedAtUtc, string? LegacyOwnerId,
    string? ImageReference, string SourceDocumentJson);

public sealed class RecipeMigrationSource
{
    public List<RecipeSourceRecord> Recipes { get; } = [];
    public List<RecipeSourceBlankPlaceholder> BlankPlaceholders { get; } = [];
    public List<RecipeSourceFieldDisposition> FieldDispositions { get; } = [];
    public List<string> UnknownFieldPaths { get; } = [];
    public List<string> BlockingDiagnostics { get; } = [];
    public List<string> Warnings { get; } = [];
    public HashSet<string> LegacyOwnerIds { get; } = new(StringComparer.Ordinal);
    public RecipeOwnerFieldCounts OwnerFieldCounts { get; set; } = new(0, 0);
    public int MissingArchiveCount { get; set; }
    public int MissingDateCount { get; set; }
    public required string SourceFingerprint { get; init; }
    public required string DecisionFingerprint { get; init; }
}
