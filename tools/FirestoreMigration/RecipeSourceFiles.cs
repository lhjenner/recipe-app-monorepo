namespace FirestoreMigration;

public sealed class RecipeSourceFiles
{
    private readonly InputFileFingerprints _fingerprints = new(
        "Recipe source, decision, manifest or image files changed after preview. Run and review again.");

    public RecipeSourceFiles(string recipePath, byte[] recipeBytes, string decisionPath,
        byte[] decisionBytes, string? imageManifestPath, byte[]? imageManifestBytes)
    {
        _fingerprints.Add(recipePath, recipeBytes);
        _fingerprints.Add(decisionPath, decisionBytes);
        if ((imageManifestPath is null) != (imageManifestBytes is null))
            throw new ArgumentException("Image manifest path and bytes must be supplied together.");
        if (imageManifestPath is not null && imageManifestBytes is not null)
            _fingerprints.Add(imageManifestPath, imageManifestBytes);
    }

    public IDisposable VerifyAndHold() => _fingerprints.VerifyAndHold();
    public void IncludeImage(string path, string sha256) => _fingerprints.Include(path, sha256);
}
