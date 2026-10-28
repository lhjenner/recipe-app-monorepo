using System.Security.Cryptography;

namespace RecipeBackup;

internal static class VerifiedCopy
{
    public static async Task CopyAsync(
        Stream source, Stream destination, long expectedBytes, string expectedHash, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long length = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, token)) != 0)
        {
            length += read;
            if (length > expectedBytes)
                throw new InvalidDataException("Backup data exceeds its recorded byte size.");
            hash.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
        }
        if (length != expectedBytes
            || Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() != expectedHash)
            throw new InvalidDataException("Backup data does not match its byte size or SHA-256.");
    }

    public static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var file = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(file, token)).ToLowerInvariant();
    }
}
