using System.Security.Cryptography;
using System.Text;

namespace FirestoreMigration;

public sealed class SourceFiles
{
    private readonly Dictionary<string, string> _fingerprints = [];
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
            _fingerprints.Add(path, Convert.ToHexString(SHA256.HashData(bytes)));
            return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        }).ToArray();
        Source = SourceParser.Parse(texts[0], texts[1], texts[2]);
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
                    throw new InvalidOperationException("Source or decisions changed after preview. Run and review again.");
            }
            return new FileHandles(handles);
        }
        catch
        {
            foreach (var stream in handles) stream.Dispose();
            throw;
        }
    }

    private sealed class FileHandles(List<FileStream> handles) : IDisposable
    {
        public void Dispose()
        {
            foreach (var handle in handles) handle.Dispose();
        }
    }
}
