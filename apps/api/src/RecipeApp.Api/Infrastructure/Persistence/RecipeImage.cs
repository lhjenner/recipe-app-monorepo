namespace RecipeApp.Api.Infrastructure.Persistence;

public sealed class RecipeImage
{
    public Guid RecipeId { get; set; }
    public Recipe? Recipe { get; set; }
    public required string ObjectKey { get; set; }
    public required string ContentType { get; set; }
    public long ByteSize { get; set; }
    public required string Sha256 { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
