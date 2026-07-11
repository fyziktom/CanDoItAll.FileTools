using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileInteractionRenderTests
{
    [Fact]
    public async Task Render_TextViewRunsLoadLifecycleAndRendersContent()
    {
        await using var renderer = new InteractionHtmlRenderer();
        var source = Source("alpha < beta", "text/plain");

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(new FileReference("test", "notes.txt"), "notes.txt"),
            source));

        Assert.Contains("data-state=\"loaded\"", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"interaction-text-view\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"group\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"tab\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-pressed=\"false\"", html, StringComparison.Ordinal);
        Assert.Contains("alpha &lt; beta", html, StringComparison.Ordinal);
        Assert.Equal(1, source.OpenCount);
    }

    [Fact]
    public async Task Render_EmptyTextShowsExplicitEmptyState()
    {
        await using var renderer = new InteractionHtmlRenderer();

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(new FileReference("test", "empty.txt"), "empty.txt"),
            Source(string.Empty, "text/plain")));

        Assert.Contains("data-testid=\"interaction-empty-file\"", html, StringComparison.Ordinal);
        Assert.Contains("This file is empty", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_DiffWithoutRegisteredRendererIsExplicitAndDoesNotReadContent()
    {
        await using var renderer = new InteractionHtmlRenderer();
        var source = Source("ignored", "text/plain");

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(
                new FileReference("test", "notes.txt"),
                "notes.txt",
                FileInteractionMode.Diff,
                "text/plain"),
            source));

        Assert.Contains("data-testid=\"interaction-unsupported\"", html, StringComparison.Ordinal);
        Assert.Contains("Diff is not available", html, StringComparison.Ordinal);
        Assert.Equal(0, source.OpenCount);
    }

    [Fact]
    public async Task Render_EditWithoutExtensionUsesLeaseMediaTypeDiscovery()
    {
        await using var renderer = new InteractionHtmlRenderer();

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(
                new FileReference("test", "README"),
                "README",
                FileInteractionMode.Edit),
            Source("editable", "text/plain")));

        Assert.Contains("data-state=\"loaded\"", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"interaction-text-editor\"", html, StringComparison.Ordinal);
        Assert.Contains(">editable</textarea>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_EditLeaseMediaTypeOverridesApproximateRequestMetadata()
    {
        await using var renderer = new InteractionHtmlRenderer();

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(
                new FileReference("test", "README"),
                "README",
                FileInteractionMode.Edit,
                mediaType: "application/octet-stream"),
            Source("source wins", "text/plain")));

        Assert.Contains("data-state=\"loaded\"", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"interaction-text-editor\"", html, StringComparison.Ordinal);
        Assert.Contains(">source wins</textarea>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_CustomProfileAndRendererProvesExtensionWithoutServiceLocation()
    {
        await using var renderer = new InteractionHtmlRenderer();
        var profile = new FileInteractionProfileDescriptor(
            "custom",
            FileInteractionCapabilities.View,
            extensions: [".foo"]);
        var composition = new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddRenderer(new FileInteractionRendererDescriptor(
                "custom-view",
                profile.Id,
                FileInteractionMode.View,
                typeof(TestFileInteractionRenderer),
                FileInteractionContentKind.Text))
            .Build();
        var parameters = Parameters(
            new FileInteractionRequest(new FileReference("test", "sample.foo"), "sample.foo"),
            Source("payload", "text/plain"));
        parameters[nameof(FileInteraction.Composition)] = composition;

        var html = await renderer.RenderAsync(parameters);

        Assert.Contains("data-testid=\"custom-renderer\"", html, StringComparison.Ordinal);
        Assert.Contains("custom:payload", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_RegisteredDiffRendererActivatesReservedDiffSeam()
    {
        await using var renderer = new InteractionHtmlRenderer();
        var profile = new FileInteractionProfileDescriptor(
            "custom-diff",
            FileInteractionCapabilities.Diff,
            extensions: [".foo"]);
        var composition = new FileInteractionComponentBuilder()
            .AddProfile(profile)
            .AddRenderer(new FileInteractionRendererDescriptor(
                "custom-diff-renderer",
                profile.Id,
                FileInteractionMode.Diff,
                typeof(TestFileInteractionRenderer),
                FileInteractionContentKind.Text))
            .Build();
        var parameters = Parameters(
            new FileInteractionRequest(
                new FileReference("test", "sample.foo"),
                "sample.foo",
                FileInteractionMode.Diff),
            Source("diff payload", "text/plain"));
        parameters[nameof(FileInteraction.Composition)] = composition;

        var html = await renderer.RenderAsync(parameters);

        Assert.Contains("data-mode=\"diff\"", html, StringComparison.Ordinal);
        Assert.Contains("custom:diff payload", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_ContentFailureShowsSafeErrorStateAndInvokesHostNotification()
    {
        await using var renderer = new InteractionHtmlRenderer();
        Exception? observed = null;
        var failure = new IOException("sensitive-provider-detail");
        var source = new DelegateContentSource((_, _) => ValueTask.FromException<FileContentLease>(failure));
        var parameters = Parameters(
            new FileInteractionRequest(new FileReference("test", "notes.txt"), "notes.txt"),
            source);
        parameters[nameof(FileInteraction.LoadFailed)] = EventCallback.Factory.Create<Exception>(
            new object(), exception => observed = exception);

        var html = await renderer.RenderAsync(parameters);

        Assert.Contains("data-testid=\"interaction-error\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive-provider-detail", html, StringComparison.Ordinal);
        Assert.Same(failure, observed);
    }

    [Fact]
    public async Task Render_UnknownFallbackNeverEmbedsPotentiallyActiveContent()
    {
        await using var renderer = new InteractionHtmlRenderer();

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(
                new FileReference("test", "payload.unknown"),
                "payload.unknown",
                mediaType: "application/x-unknown"),
            Source("<script>danger()</script>", "application/x-unknown")));

        Assert.Contains("data-testid=\"interaction-object-view\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<object", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("danger()", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_InertUnknownFallbackReadsMetadataButNeverPayload()
    {
        await using var renderer = new InteractionHtmlRenderer();
        var stream = new ReadRejectingStream();
        var source = new DelegateContentSource((_, _) => ValueTask.FromResult(
            new FileContentLease(
                stream,
                "application/x-unknown",
                length: 50_000_000,
                revision: new FileContentRevision("r1"))));

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(
                new FileReference("test", "payload.unknown"),
                "payload.unknown",
                mediaType: "application/octet-stream"),
            source));

        Assert.Contains("data-testid=\"interaction-object-view\"", html, StringComparison.Ordinal);
        Assert.Equal(1, source.OpenCount);
        Assert.Equal(0, stream.ReadCount);
        Assert.True(stream.WasDisposed);
    }

    [Theory]
    [InlineData("hostile.svg", null)]
    [InlineData("hostile.bin", "image/svg+xml")]
    public async Task Render_SvgUsesInertMetadataOnlySurfaceAndNeverEmbedsOrReadsPayload(
        string fileName,
        string? requestMediaType)
    {
        await using var renderer = new InteractionHtmlRenderer();
        var stream = new ReadRejectingStream();
        var source = new DelegateContentSource((_, _) => ValueTask.FromResult(
            new FileContentLease(
                stream,
                "image/svg+xml",
                length: 10_000,
                revision: new FileContentRevision("r1"))));

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(
                new FileReference("test", fileName),
                fileName,
                mediaType: requestMediaType),
            source));

        Assert.Contains("data-testid=\"interaction-object-view\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, stream.ReadCount);
        Assert.True(stream.WasDisposed);
    }

    [Theory]
    [InlineData("photo.heic", "image/heic")]
    [InlineData("scan.tiff", "image/tiff")]
    public async Task Render_UnsupportedImageMediaTypeUsesInertFallback(
        string fileName,
        string mediaType)
    {
        await using var renderer = new InteractionHtmlRenderer();
        var stream = new ReadRejectingStream();
        var source = new DelegateContentSource((_, _) => ValueTask.FromResult(
            new FileContentLease(stream, mediaType, length: 50_000)));

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(
                new FileReference("test", fileName),
                fileName,
                mediaType: mediaType),
            source));

        Assert.Contains("data-testid=\"interaction-object-view\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"interaction-image-view\"", html, StringComparison.Ordinal);
        Assert.Equal(0, stream.ReadCount);
    }

    [Theory]
    [InlineData("photo.png", "image/png", "interaction-image-view", "<img")]
    [InlineData("report.pdf", "application/pdf", "interaction-pdf-view", "<object")]
    public async Task Render_BinaryBuiltInsUseDedicatedBoundedViewers(
        string fileName,
        string mediaType,
        string testId,
        string expectedElement)
    {
        await using var renderer = new InteractionHtmlRenderer();

        var html = await renderer.RenderAsync(Parameters(
            new FileInteractionRequest(new FileReference("test", fileName), fileName, mediaType: mediaType),
            Source("bounded-payload", mediaType)));

        Assert.Contains($"data-testid=\"{testId}\"", html, StringComparison.Ordinal);
        Assert.Contains(expectedElement, html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:text", html, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> Parameters(
        FileInteractionRequest request,
        IFileContentSource source)
        => new()
        {
            [nameof(FileInteraction.Request)] = request,
            [nameof(FileInteraction.ContentSource)] = source
        };

    private static DelegateContentSource Source(string content, string mediaType)
        => new((_, _) =>
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream(bytes, writable: false),
                mediaType,
                bytes.Length));
        });

    private sealed class InteractionHtmlRenderer : IAsyncDisposable
    {
        private readonly ServiceProvider services;
        private readonly HtmlRenderer renderer;

        public InteractionHtmlRenderer()
        {
            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton<IJSRuntime, StaticHtmlJsRuntime>();
            services = collection.BuildServiceProvider();
            renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        }

        public Task<string> RenderAsync(IDictionary<string, object?> parameters)
            => renderer.Dispatcher.InvokeAsync(async () =>
            {
                var root = await renderer.RenderComponentAsync<FileInteraction>(
                    ParameterView.FromDictionary(parameters));
                return root.ToHtmlString();
            });

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await services.DisposeAsync();
        }
    }

    private sealed class StaticHtmlJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromException<TValue>(new InvalidOperationException("JS is unavailable during static rendering."));

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
            => ValueTask.FromException<TValue>(new InvalidOperationException("JS is unavailable during static rendering."));
    }

    private sealed class ReadRejectingStream : Stream
    {
        public int ReadCount { get; private set; }
        public bool WasDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 50_000_000;
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            throw new InvalidOperationException("Metadata-only rendering must not read payload bytes.");
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return ValueTask.FromException<int>(
                new InvalidOperationException("Metadata-only rendering must not read payload bytes."));
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return base.DisposeAsync();
        }
    }
}
