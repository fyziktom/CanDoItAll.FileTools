namespace CanDoItAll.FileTools.Providers.FileSystem;

/// <summary>An owned read-only view that cannot read or seek beyond one requested file range.</summary>
internal sealed class FileSystemRangeReadStream : Stream
{
    private readonly Stream inner;
    private readonly long rangeStart;
    private readonly long rangeLength;
    private long position;
    private bool disposed;

    public FileSystemRangeReadStream(Stream inner, long rangeStart, long rangeLength)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (!inner.CanRead || !inner.CanSeek)
        {
            throw new ArgumentException("The range stream requires a readable, seekable stream.", nameof(inner));
        }

        if (rangeStart < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rangeStart));
        }

        if (rangeLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rangeLength));
        }

        this.rangeStart = rangeStart;
        this.rangeLength = rangeLength;
        inner.Seek(rangeStart, SeekOrigin.Begin);
    }

    public override bool CanRead => !disposed && inner.CanRead;

    public override bool CanSeek => !disposed && inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length
    {
        get
        {
            ThrowIfDisposed();
            return rangeLength;
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfDisposed();
            return position;
        }
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush()
        => ThrowIfDisposed();

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        ThrowIfDisposed();
        var boundedCount = GetBoundedCount(count);
        if (boundedCount == 0)
        {
            return 0;
        }

        var read = inner.Read(buffer, offset, boundedCount);
        position += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfDisposed();
        var boundedCount = GetBoundedCount(buffer.Length);
        if (boundedCount == 0)
        {
            return 0;
        }

        var read = inner.Read(buffer[..boundedCount]);
        position += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var boundedCount = GetBoundedCount(buffer.Length);
        if (boundedCount == 0)
        {
            return 0;
        }

        var read = await inner.ReadAsync(buffer[..boundedCount], cancellationToken).ConfigureAwait(false);
        position += read;
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadArrayAsync(buffer, offset, count, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfDisposed();
        long target;
        try
        {
            target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(position + offset),
                SeekOrigin.End => checked(rangeLength + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
        }
        catch (OverflowException exception)
        {
            throw new IOException("The requested range position is outside the stream.", exception);
        }

        if (target < 0 || target > rangeLength)
        {
            throw new IOException("The requested range position is outside the stream.");
        }

        inner.Seek(checked(rangeStart + target), SeekOrigin.Begin);
        position = target;
        return position;
    }

    public override void SetLength(long value)
        => throw new NotSupportedException("The range stream is read-only.");

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("The range stream is read-only.");

    public override void Write(ReadOnlySpan<byte> buffer)
        => throw new NotSupportedException("The range stream is read-only.");

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            await inner.DisposeAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }

    private async Task<int> ReadArrayAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var boundedCount = GetBoundedCount(count);
        if (boundedCount == 0)
        {
            return 0;
        }

        var read = await inner.ReadAsync(buffer.AsMemory(offset, boundedCount), cancellationToken).ConfigureAwait(false);
        position += read;
        return read;
    }

    private int GetBoundedCount(int requestedCount)
        => (int)Math.Min(requestedCount, rangeLength - position);

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(disposed, this);
}
