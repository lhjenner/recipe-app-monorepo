using RecipeApp.Api.Images;

namespace RecipeApp.Api.ComponentTests.Migration;

internal sealed class MigrationImageStorage : IPrivateObjectMigrationStorage
{
    public string Scope => "synthetic-private-migration";
    public Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);
    public int Creates { get; private set; }
    public int Deletes { get; private set; }
    public bool CorruptWrites { get; set; }
    public bool FailAfterWrite { get; set; }
    public Func<Task>? AfterCreate { get; set; }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Stream?>(Objects.TryGetValue(objectKey, out var bytes)
            ? new MemoryStream(bytes, writable: false) : null);
    }

    public async Task CreateOnlyAsync(string objectKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        if (!Objects.TryAdd(objectKey, CorruptWrites ? [1, 2, 3] : buffer.ToArray()))
            throw new IOException("Synthetic create-only conflict.");
        Creates++;
        if (AfterCreate is not null) await AfterCreate();
        if (FailAfterWrite) throw new IOException("Synthetic uncertain upload response.");
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Objects.Remove(objectKey);
        Deletes++;
        return Task.CompletedTask;
    }

    public async Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Objects[objectKey] = buffer.ToArray();
    }
}
