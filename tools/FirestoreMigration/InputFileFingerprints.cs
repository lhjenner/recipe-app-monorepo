using System.Security.Cryptography;

namespace FirestoreMigration;

internal sealed class InputFileFingerprints(string changedMessage)
{
    private readonly Dictionary<string, string> _fingerprints = new(StringComparer.OrdinalIgnoreCase);

    public void Add(string path, byte[] bytes) =>
        _fingerprints.Add(path, Convert.ToHexString(SHA256.HashData(bytes)));

    public void Include(string path, string sha256)
    {
        if (_fingerprints.TryGetValue(path, out var existing) && existing != sha256)
            throw new InvalidDataException("Input file has conflicting checksums.");
        _fingerprints[path] = sha256;
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
                    throw new InvalidOperationException(changedMessage);
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
