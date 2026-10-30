using System.Security.Cryptography;
using Amazon.Runtime;
using Microsoft.EntityFrameworkCore;
using RecipeApp.Api.Infrastructure.Persistence;

namespace RecipeApp.Api.Images;

public sealed class RecipeImageReadService(
    IDbContextFactory<AppDbContext> databaseFactory,
    ICurrentUser currentUser,
    IPrivateObjectStorage storage) : IRecipeImageReadService
{
    public async Task<RecipeImageContent?> GetAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        await using var db = await databaseFactory.CreateDbContextAsync(cancellationToken);
        var image = await db.RecipeImages.AsNoTracking()
            .Where(row => row.RecipeId == recipeId && row.Recipe!.OwnerUserId == currentUser.UserId
                && !row.Recipe.IsArchived)
            .SingleOrDefaultAsync(cancellationToken);
        if (image is null) return null;

        try
        {
            if (image.ByteSize is <= 0 or > int.MaxValue
                || image.ContentType is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp"))
                throw new InvalidDataException("Invalid stored image metadata.");

            await using var stream = await storage.OpenReadAsync(image.ObjectKey, cancellationToken)
                ?? throw new InvalidDataException("A referenced private image object is missing.");
            var bytes = new byte[(int)image.ByteSize];
            await stream.ReadExactlyAsync(bytes, cancellationToken);
            var extra = new byte[1];
            if (await stream.ReadAsync(extra, cancellationToken) != 0
                || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(image.Sha256, StringComparison.OrdinalIgnoreCase)
                || !HasExpectedSignature(bytes, image.ContentType))
                throw new InvalidDataException("Private image bytes do not match their metadata.");

            return new RecipeImageContent(bytes, image.ContentType, image.Sha256.ToLowerInvariant());
        }
        catch (Exception exception) when (exception is AmazonServiceException or IOException
            or InvalidDataException or HttpRequestException)
        {
            throw new ImageStorageUnavailableException(exception);
        }
    }

    private static bool HasExpectedSignature(ReadOnlySpan<byte> bytes, string contentType) => contentType switch
    {
        "image/png" => bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/jpeg" => bytes.StartsWith(new byte[] { 255, 216, 255 }),
        "image/gif" => bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8),
        "image/webp" => bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };
}
