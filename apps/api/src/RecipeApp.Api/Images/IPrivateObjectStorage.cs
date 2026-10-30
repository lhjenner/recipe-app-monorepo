namespace RecipeApp.Api.Images;

/// <summary>
/// Private object storage keyed by opaque keys, never by public URLs.
/// </summary>
public interface IPrivateObjectStorage
{
    /// <summary>
    /// Opens a private object for reading, or returns null when the key does not exist.
    /// The caller owns and must dispose the returned stream.
    /// </summary>
    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the stream from its current position to the end, replacing any existing object at the key.
    /// The caller retains ownership of the input stream.
    /// </summary>
    Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default);
}
