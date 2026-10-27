namespace RecipeApp.Api.Images;

public interface IRecipeImageReadService
{
    Task<RecipeImageContent?> GetAsync(Guid recipeId, CancellationToken cancellationToken);
}

public sealed record RecipeImageContent(byte[] Bytes, string ContentType, string Sha256);
