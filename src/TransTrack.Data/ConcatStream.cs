namespace TransTrack.Data;

/// <summary>
/// Two streams read as one, used to put back bytes already consumed.
///
/// Detecting a file's format means reading its first bytes. When the upload
/// arrives on a rewindable stream that costs nothing — seek back to zero. A
/// raw, forward-only request body cannot be rewound, so the header is stitched
/// on the front instead and the file still gets written whole.
///
/// Read-only and forward-only by design: it exists to be copied to disk once.
/// </summary>
internal sealed class ConcatStream(Stream first, Stream second) : Stream
{
    private bool _firstDone;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (!_firstDone)
        {
            var read = first.Read(buffer);
            if (read > 0) return read;
            _firstDone = true;
        }

        return second.Read(buffer);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        if (!_firstDone)
        {
            var read = await first.ReadAsync(buffer, ct);
            if (read > 0) return read;
            _firstDone = true;
        }

        return await second.ReadAsync(buffer, ct);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            first.Dispose();
            second.Dispose();
        }

        base.Dispose(disposing);
    }
}
