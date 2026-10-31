using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RecipeApp.Api.Infrastructure.Storage;

namespace RecipeApp.Api.UnitTests.Images;

public sealed class S3PrivateObjectStorageTests
{
    private readonly IAmazonS3 _client = Substitute.For<IAmazonS3>();
    private readonly ObjectStorageOptions _options = new() { BucketName = "synthetic-tests" };

    [Fact]
    public async Task Read_UsesThePrivateBucketAndOwnsTheResponseStream()
    {
        var content = new MemoryStream([1, 2, 3]);
        _client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetObjectResponse { ResponseStream = content });
        var storage = new S3PrivateObjectStorage(_client, Options.Create(_options));
        using var cancellation = new CancellationTokenSource();

        var stream = await storage.OpenReadAsync("recipes/test", cancellation.Token);
        stream.Should().NotBeNull();
        await stream!.DisposeAsync();

        content.CanRead.Should().BeFalse();
        await _client.Received(1).GetObjectAsync(
            Arg.Is<GetObjectRequest>(request => request.BucketName == _options.BucketName
                && request.Key == "recipes/test"), cancellation.Token);
    }

    [Fact]
    public async Task Read_ReturnsNullOnlyForNoSuchKey()
    {
        _client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("Missing")
            {
                StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey"
            });
        var storage = new S3PrivateObjectStorage(_client, Options.Create(_options));

        (await storage.OpenReadAsync("missing")).Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "AccessDenied")]
    [InlineData(HttpStatusCode.NotFound, "NoSuchBucket")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "SlowDown")]
    public async Task Read_DoesNotDisguiseProviderFailuresAsMissingObjects(HttpStatusCode status, string code)
    {
        _client.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("Storage failure") { StatusCode = status, ErrorCode = code });
        var storage = new S3PrivateObjectStorage(_client, Options.Create(_options));

        var act = () => storage.OpenReadAsync("recipes/test");

        await act.Should().ThrowAsync<AmazonS3Exception>();
    }

    [Fact]
    public async Task Write_PassesTheCallerStreamWithoutClosingOrRewindingIt()
    {
        using var content = new MemoryStream([0, 1, 2]);
        content.Position = 1;
        using var cancellation = new CancellationTokenSource();
        var storage = new S3PrivateObjectStorage(_client, Options.Create(_options));

        await storage.WriteAsync("recipes/test", content, cancellation.Token);

        await _client.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(request =>
            request.BucketName == _options.BucketName && request.Key == "recipes/test"
            && request.InputStream == content && !request.AutoCloseStream
            && !request.AutoResetStreamPosition && !request.UseChunkEncoding), cancellation.Token);
        content.CanRead.Should().BeTrue();
        content.Position.Should().Be(1);
    }

    [Fact]
    public async Task CreateOnly_RequiresConditionalCreationAndExplicitContentType()
    {
        using var content = new MemoryStream([1, 2, 3]);
        var storage = new S3PrivateObjectStorage(_client, Options.Create(_options));
        await storage.CreateOnlyAsync("imports/opaque", content, "image/png");
        await _client.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(request =>
            request.IfNoneMatch == "*" && request.BucketName == _options.BucketName
            && request.Key == "imports/opaque" && request.ContentType == "image/png"
            && !request.AutoCloseStream && !request.UseChunkEncoding), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_IsLimitedToTheExplicitKey()
    {
        var storage = new S3PrivateObjectStorage(_client, Options.Create(_options));
        await storage.DeleteAsync("imports/opaque");
        await _client.Received(1).DeleteObjectAsync(Arg.Is<DeleteObjectRequest>(request =>
            request.Key == "imports/opaque" && request.BucketName == _options.BucketName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOnly_PropagatesPreconditionFailureWithoutOverwriteFallback()
    {
        _client.PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonS3Exception("Already exists") { StatusCode = HttpStatusCode.PreconditionFailed });
        var storage = new S3PrivateObjectStorage(_client, Options.Create(_options));
        using var content = new MemoryStream([1, 2, 3]);
        var create = () => storage.CreateOnlyAsync("imports/existing", content, "image/png");
        await create.Should().ThrowAsync<AmazonS3Exception>();
        await _client.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(request => request.IfNoneMatch == "*"),
            Arg.Any<CancellationToken>());
    }
}
