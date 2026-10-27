using RecipeApp.Api.Images;

namespace RecipeApp.Api.ComponentTests.Images;

public sealed class SyntheticObjectStorage : IPrivateObjectStorage
{
    public static byte[] ImageBytes => TestFixtures.SyntheticImage.Png;

    public byte[]? Content { get; set; }
    public bool Unavailable { get; set; }
    public int Reads { get; private set; }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reads++;
        if (Unavailable) throw new IOException("Synthetic storage outage.");
        return Task.FromResult<Stream?>(Content is null ? null : new MemoryStream(Content, writable: false));
    }

    public async Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Content = buffer.ToArray();
    }
}
