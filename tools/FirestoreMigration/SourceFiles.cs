using System.Text;

namespace FirestoreMigration;

public sealed class SourceFiles
{
    private readonly InputFileFingerprints _fingerprints = new(
        "Source or decisions changed after preview. Run and review again.");
    public MigrationSource Source { get; }

    public SourceFiles(MigrationOptions options)
    {
        var paths = new[] { Path.Combine(options.Backup, "ingredients.json"),
            Path.Combine(options.Backup, "meta.json"), options.Decisions };
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
            throw new FormatException("The decision file must be separate from both backup files.");
        var texts = paths.Select(path =>
        {
            var bytes = File.ReadAllBytes(path);
            _fingerprints.Add(path, bytes);
            return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        }).ToArray();
        Source = SourceParser.Parse(texts[0], texts[1], texts[2]);
    }

    public IDisposable VerifyAndHold() => _fingerprints.VerifyAndHold();
}
