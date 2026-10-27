using System.Collections.Concurrent;
using FluentAssertions;
using RecipeApp.Api.Images;

namespace RecipeApp.Api.UnitTests.Images;

public sealed class PrivateObjectStorageTests
{
    private const string ObjectKey = "recipes/synthetic-recipe/image.png";
    private static readonly byte[] SyntheticImage = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");

    [Fact]
    public async Task WriteThenRead_PreservesTheExactObjectBytes()
    {
        var storage = new InMemoryPrivateObjectStorage();
        await using var content = new MemoryStream(SyntheticImage);

        await storage.WriteAsync(ObjectKey, content);

        content.CanRead.Should().BeTrue();
        await using var stored = await storage.OpenReadAsync(ObjectKey);
        stored.Should().NotBeNull();
        await using var actual = new MemoryStream();
        await stored!.CopyToAsync(actual);
        actual.ToArray().Should().Equal(SyntheticImage);
    }

    [Fact]
    public async Task WriteAsync_ReplacesAnExistingObject()
    {
        var storage = new InMemoryPrivateObjectStorage();
        await using var original = new MemoryStream([1, 2, 3]);
        await using var replacement = new MemoryStream([4, 5]);
        await storage.WriteAsync(ObjectKey, original);

        await storage.WriteAsync(ObjectKey, replacement);

        await using var stored = await storage.OpenReadAsync(ObjectKey);
        stored.Should().NotBeNull();
        await using var actual = new MemoryStream();
        await stored!.CopyToAsync(actual);
        actual.ToArray().Should().Equal(4, 5);
    }

    [Fact]
    public async Task OpenReadAsync_ReturnsNullWhenTheObjectDoesNotExist()
    {
        var storage = new InMemoryPrivateObjectStorage();

        var result = await storage.OpenReadAsync(ObjectKey);

        result.Should().BeNull();
    }

    [Fact]
    public async Task StorageInstances_DoNotShareObjects()
    {
        var firstStorage = new InMemoryPrivateObjectStorage();
        var secondStorage = new InMemoryPrivateObjectStorage();
        await using var content = new MemoryStream(SyntheticImage);
        await firstStorage.WriteAsync(ObjectKey, content);

        var result = await secondStorage.OpenReadAsync(ObjectKey);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Operations_ObserveCancellation()
    {
        var storage = new InMemoryPrivateObjectStorage();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await using var content = new MemoryStream(SyntheticImage);

        var write = () => storage.WriteAsync(ObjectKey, content, cancellation.Token);
        var read = () => storage.OpenReadAsync(ObjectKey, cancellation.Token);

        await write.Should().ThrowAsync<OperationCanceledException>();
        await read.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class InMemoryPrivateObjectStorage : IPrivateObjectStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

        public Task<Stream?> OpenReadAsync(
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
            cancellationToken.ThrowIfCancellationRequested();

            Stream? result = _objects.TryGetValue(objectKey, out var content)
                ? new MemoryStream(content, writable: false)
                : null;
            return Task.FromResult(result);
        }

        public async Task WriteAsync(
            string objectKey,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
            ArgumentNullException.ThrowIfNull(content);
            cancellationToken.ThrowIfCancellationRequested();

            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _objects[objectKey] = buffer.ToArray();
        }
    }
}
