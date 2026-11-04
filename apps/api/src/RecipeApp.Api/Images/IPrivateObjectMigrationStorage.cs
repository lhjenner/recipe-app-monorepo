namespace RecipeApp.Api.Images;

public interface IPrivateObjectMigrationStorage : IPrivateObjectStorage
{
    string Scope { get; }
    Task CreateOnlyAsync(string objectKey, Stream content, string contentType,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
