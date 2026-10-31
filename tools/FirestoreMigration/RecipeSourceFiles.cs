using System.Security.Cryptography;

namespace FirestoreMigration;

public sealed class RecipeSourceFiles
{
    private readonly Dictionary<string, string> _fingerprints = new(StringComparer.OrdinalIgnoreCase);

    public RecipeSourceFiles(string recipePath, byte[] recipeBytes, string decisionPath,
        byte[] decisionBytes, string? imageManifestPath, byte[]? imageManifestBytes)
    {
        Add(recipePath, recipeBytes);
        Add(decisionPath, decisionBytes);
        if ((imageManifestPath is null) != (imageManifestBytes is null))
            throw new ArgumentException("Image manifest path and bytes must be supplied together.");
        if (imageManifestPath is not null && imageManifestBytes is not null)
            Add(imageManifestPath, imageManifestBytes);
    }

    public IDisposable VerifyAndHold()
    {
        var handles = new List<FileStream>();
        try
        {
            foreach (var (path, fingerprint) in _fingerprints)
            {
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                handles.Add(stream);
                if (Convert.ToHexString(SHA256.HashData(stream)) != fingerprint)
                    throw new InvalidOperationException(
                        "Recipe source or decision files changed after preview. Run and review again.");
            }
            return new FileHandles(handles);
        }
        catch
        {
            foreach (var stream in handles)
                stream.Dispose();
            throw;
        }
    }

    private void Add(string path, byte[] bytes)
    {
        if (!_fingerprints.TryAdd(path, Convert.ToHexString(SHA256.HashData(bytes))))
            throw new ArgumentException("Recipe input files must have distinct paths.");
    }

    private sealed class FileHandles(List<FileStream> handles) : IDisposable
    {
        public void Dispose()
        {
            foreach (var handle in handles)
                handle.Dispose();
        }
    }
}
