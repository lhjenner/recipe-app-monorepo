using Amazon.S3.Model;

namespace RecipeApp.Api.Infrastructure.Storage;

internal sealed class S3ObjectReadStream(GetObjectResponse response) : Stream
{
    private Stream Inner => response.ResponseStream;

    public override bool CanRead => Inner.CanRead;
    public override bool CanSeek => Inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => Inner.Length;
    public override long Position { get => Inner.Position; set => Inner.Position = value; }
    public override void Flush() => Inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => Inner.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Inner.ReadAsync(buffer, cancellationToken);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Inner.ReadAsync(buffer, offset, count, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) response.Dispose();
        base.Dispose(disposing);
    }
}
