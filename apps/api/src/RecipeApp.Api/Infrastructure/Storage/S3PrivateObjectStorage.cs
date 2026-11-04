using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using RecipeApp.Api.Images;

namespace RecipeApp.Api.Infrastructure.Storage;

public sealed class S3PrivateObjectStorage(
    IAmazonS3 client,
    IOptions<ObjectStorageOptions> options) : IPrivateObjectMigrationStorage
{
    public string Scope => $"{client.Config.ServiceURL}|{options.Value.BucketName}";

    public async Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = options.Value.BucketName,
                Key = objectKey
            }, cancellationToken);
            return new S3ObjectReadStream(response);
        }
        catch (AmazonS3Exception exception) when (exception.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task WriteAsync(string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentNullException.ThrowIfNull(content);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = "application/octet-stream",
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            UseChunkEncoding = false
        }, cancellationToken);
    }

    public async Task CreateOnlyAsync(string objectKey, Stream content, string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            IfNoneMatch = "*",
            AutoCloseStream = false,
            AutoResetStreamPosition = false,
            UseChunkEncoding = false
        }, cancellationToken);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = options.Value.BucketName,
            Key = objectKey
        }, cancellationToken);
    }
}
