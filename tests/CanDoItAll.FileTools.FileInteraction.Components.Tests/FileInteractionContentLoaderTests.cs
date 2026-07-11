using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileInteractionContentLoaderTests
{
    private static readonly FileReference File = new("test", "file");

    [Fact]
    public async Task LoadAsync_LeaseMetadataOverridesRequestAndLeaseIsDisposed()
    {
        var stream = new TrackingMemoryStream(Encoding.UTF8.GetBytes("hello"));
        var source = new DelegateContentSource((_, _) => ValueTask.FromResult(
            new FileContentLease(
                stream,
                "text/plain",
                stream.Length,
                new FileContentRevision("lease-r2"))));
        var request = new FileInteractionRequest(
            File, "file.txt", mediaType: "application/json", contentRevision: new FileContentRevision("request-r1"));

        var loaded = await new FileInteractionContentLoader().LoadAsync(source, request, 1024);

        Assert.Equal("hello", Encoding.UTF8.GetString(loaded.Content.Span));
        Assert.Equal("text/plain", loaded.MediaType);
        Assert.Equal("lease-r2", loaded.Revision?.Value);
        Assert.True(stream.WasDisposed);
    }

    [Fact]
    public async Task LoadAsync_AbsentLeaseRevisionRetainsHostSuppliedRevision()
    {
        var requestRevision = new FileContentRevision("host-r1");
        var source = Source("content", mediaType: null, revision: null);
        var request = new FileInteractionRequest(
            File, "file.txt", contentRevision: requestRevision);

        var loaded = await new FileInteractionContentLoader().LoadAsync(source, request, 1024);

        Assert.Equal(requestRevision, loaded.Revision);
    }

    [Fact]
    public async Task LoadAsync_StreamBeyondLimitThrowsWithoutPublishingPartialContent()
    {
        var source = Source(new string('x', 33), length: null);

        var exception = await Assert.ThrowsAsync<FileInteractionContentTooLargeException>(async () =>
            await new FileInteractionContentLoader().LoadAsync(
                source,
                new FileInteractionRequest(File, "file.txt"),
                maximumBytes: 32));

        Assert.Equal(32, exception.MaximumBytes);
    }

    [Fact]
    public async Task LoadAsync_IntMaxBoundaryUsesLongRequestAndBoundedInitialBuffer()
    {
        FileContentReadRequest? observed = null;
        var source = new DelegateContentSource((request, _) =>
        {
            observed = request;
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream(), length: int.MaxValue));
        });

        var loaded = await new FileInteractionContentLoader().LoadAsync(
            source,
            new FileInteractionRequest(File, "empty.bin"),
            int.MaxValue);

        Assert.Empty(loaded.Content.ToArray());
        Assert.Equal((long)int.MaxValue + 1, observed!.Length);
    }

    [Fact]
    public async Task LoadAsync_CancellationDuringReadIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new DelegateContentSource((_, _) => ValueTask.FromResult(
            new FileContentLease(new CancellationStream())));
        var task = new FileInteractionContentLoader().LoadAsync(
            source,
            new FileInteractionRequest(File, "file.txt"),
            1024,
            cancellation.Token).AsTask();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void TextBuffer_Utf8BomIsPreservedAcrossEdit()
    {
        var input = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("before")).ToArray();

        var buffer = FileInteractionTextBuffer.Decode(input);
        var output = buffer.Encode("after");

        Assert.Equal("before", buffer.Text);
        Assert.True(output.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        Assert.Equal("after", Encoding.UTF8.GetString(output.AsSpan(Encoding.UTF8.GetPreamble().Length)));
    }

    [Fact]
    public void TextBuffer_InvalidUtf8IsRejectedAsContentError()
    {
        Assert.Throws<InvalidDataException>(() =>
            FileInteractionTextBuffer.Decode(new byte[] { 0xC3, 0x28 }));
    }

    private static DelegateContentSource Source(
        string content,
        string? mediaType = "text/plain",
        FileContentRevision? revision = null,
        long? length = null)
        => new((_, _) =>
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream(bytes, writable: false),
                mediaType,
                length,
                revision));
        });

    private sealed class TrackingMemoryStream(byte[] content) : MemoryStream(content)
    {
        public bool WasDisposed { get; private set; }

        public override ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return base.DisposeAsync();
        }
    }

    private sealed class CancellationStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}

internal sealed class DelegateContentSource(
    Func<FileContentReadRequest, CancellationToken, ValueTask<FileContentLease>> open) : IFileContentSource
{
    public int OpenCount { get; private set; }

    public ValueTask<FileContentLease> OpenReadAsync(
        FileContentReadRequest request,
        CancellationToken cancellationToken = default)
    {
        OpenCount++;
        return open(request, cancellationToken);
    }
}
