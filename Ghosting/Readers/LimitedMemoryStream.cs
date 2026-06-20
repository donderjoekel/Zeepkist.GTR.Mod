using System;
using System.IO;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

internal sealed class LimitedMemoryStream : Stream
{
    private readonly MemoryStream _inner = new();
    private readonly long _maxLength;

    public LimitedMemoryStream(long maxLength)
    {
        _maxLength = maxLength;
    }

    public byte[] ToArray()
    {
        return _inner.ToArray();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        EnsureCapacity(count);
        _inner.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _inner.WriteByte(value);
    }

    private void EnsureCapacity(int additionalBytes)
    {
        if (_inner.Length + additionalBytes > _maxLength)
            throw new InvalidDataException($"Ghost data exceeds {_maxLength} byte limit.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value)
    {
        if (value > _maxLength)
            throw new InvalidDataException($"Ghost data exceeds {_maxLength} byte limit.");
        _inner.SetLength(value);
    }
}
