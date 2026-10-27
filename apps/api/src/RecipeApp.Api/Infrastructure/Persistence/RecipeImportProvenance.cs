namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class RecipeImportProvenance
{
    public Guid RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public required string SourceDocumentJson { get; set; }
    public required string DocumentSha256 { get; set; }
    public required string SourceFileSha256 { get; set; }
    public required string MappingVersion { get; set; }
    public required string DecisionFingerprint { get; set; }
    public Guid LastAppliedRunId { get; set; }
    public DateTime LastAppliedAtUtc { get; set; }
}
