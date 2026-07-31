using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileObjectUrlInteropTests : FileToolsBunitContext
{
    [Fact]
    public void ContentStamp_NewBackingBytesForSameMetadataRequiresRefresh()
    {
        var file = new FileReference("test", "image.png");
        var revision = new FileContentRevision("r1");
        var first = new FileObjectContentStamp(
            file, 0, 3, new byte[] { 1, 2, 3 }, revision, "image/png", FileObjectViewKind.Image);
        var second = new FileObjectContentStamp(
            file, 0, 3, new byte[] { 1, 2, 3 }, revision, "image/png", FileObjectViewKind.Image);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task ApplyAndDispose_UseCollocatedModuleAndRevokeOwnedUrl()
    {
        var module = new RecordingJsObjectReference();
        var runtime = new RecordingJsRuntime(module);
        var interop = new FileObjectUrlInterop(runtime);

        await interop.ApplyAsync(default(ElementReference), new byte[] { 1, 2, 3 }, "image/png", "src");
        await interop.DisposeAsync(default);
        await interop.DisposeAsync(default);

        Assert.Equal(FileObjectUrlInterop.ImportMethod, runtime.Identifier);
        Assert.Equal(FileObjectUrlInterop.ModulePath, Assert.Single(runtime.Arguments!));
        Assert.Equal(
            [FileObjectUrlInterop.ApplyMethod, FileObjectUrlInterop.RevokeMethod],
            module.Invocations.Select(invocation => invocation.Identifier));
        Assert.True(module.WasDisposed);
    }

    [Fact]
    public void ApplyFailure_RendersAccessibleInertFallbackWithoutEscapingTheRenderCycle()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true)
            .SetException(new JSException("Blob URLs are unavailable."));
        var request = new FileInteractionRequest(
            new FileReference("test", "image.png"),
            "image.png",
            mediaType: "image/png");
        var context = new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            new byte[] { 1, 2, 3 },
            editRevision: 0,
            mediaType: "image/png");

        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Image));

        cut.WaitForAssertion(() =>
        {
            var fallback = cut.Find("[data-testid='interaction-object-fallback']");
            Assert.Equal("status", fallback.GetAttribute("role"));
            Assert.Contains("host-provided action", fallback.TextContent, StringComparison.Ordinal);
            Assert.True(cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden"));
        });
    }

    [Fact]
    public void NonEmptyToEmpty_RevokesThePreviousObjectUrlBeforeRetainingHiddenTarget()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var revoke = module.SetupVoid(FileObjectUrlInterop.RevokeMethod, _ => true).SetVoidResult();
        var request = new FileInteractionRequest(
            new FileReference("test", "image.png"),
            "image.png",
            mediaType: "image/png");
        var populated = new FileInteractionRenderContext(
            request, FileInteractionMode.View, new byte[] { 1 }, 0, "image/png");
        var empty = new FileInteractionRenderContext(
            request, FileInteractionMode.View, ReadOnlyMemory<byte>.Empty, 1, "image/png");
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, populated)
            .Add(component => component.Kind, FileObjectViewKind.Image));

        cut.Render(parameters => parameters
            .Add(component => component.Context, empty)
            .Add(component => component.Kind, FileObjectViewKind.Image));

        Assert.Single(revoke.Invocations);
        Assert.NotNull(cut.Find("[data-testid='interaction-empty-file']"));
        Assert.True(cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden"));
    }

    [Fact]
    public async Task Dispose_WhenRevokeFailsStillDisposesModuleAndDoesNotThrow()
    {
        var module = new RecordingJsObjectReference
        {
            RevokeError = new JSDisconnectedException("Circuit disconnected.")
        };
        var interop = new FileObjectUrlInterop(new RecordingJsRuntime(module));
        await interop.ApplyAsync(default, new byte[] { 1 }, "image/png", "src");

        await interop.DisposeAsync(default);

        Assert.True(module.WasDisposed);
    }

    [Fact]
    public async Task OverlappingApplyRequests_AreSerializedAndLatestContentWins()
    {
        var module = new ControlledApplyJsObjectReference();
        var interop = new FileObjectUrlInterop(new RecordingJsRuntime(module));

        var first = interop.ApplyAsync(default, new byte[] { 1 }, "image/png", "src").AsTask();
        await module.FirstApplyEntered.Task.WaitAsync(AsyncOperationTimeout);
        var second = interop.ApplyAsync(default, new byte[] { 2 }, "image/png", "src").AsTask();

        Assert.Equal(1, module.ApplyCount);
        module.ReleaseNextApply();
        await module.SecondApplyEntered.Task.WaitAsync(AsyncOperationTimeout);

        Assert.False(await first);
        Assert.Equal(2, module.ApplyCount);
        module.ReleaseNextApply();
        Assert.True(await second);
        Assert.Equal(1, module.MaximumConcurrency);
        Assert.Equal(
            [
                FileObjectUrlInterop.ApplyMethod,
                FileObjectUrlInterop.RevokeMethod,
                FileObjectUrlInterop.ApplyMethod
            ],
            module.InvocationIdentifiers);
        Assert.Equal(
            new byte[] { 2 },
            Assert.IsType<byte[]>(module.ApplyArguments[1][1]));

        await interop.DisposeAsync(default);
    }

    [Fact]
    public async Task ContentReplacement_HidesPriorObjectUntilLatestUrlIsApplied()
    {
        var module = new DelayedReplacementJsObjectReference();
        Services.AddSingleton<IJSRuntime>(new RecordingJsRuntime(module));
        var request = new FileInteractionRequest(
            new FileReference("test", "image.png"),
            "image.png",
            mediaType: "image/png");
        var first = new FileInteractionRenderContext(
            request, FileInteractionMode.View, new byte[] { 1 }, 0, "image/png");
        var second = new FileInteractionRenderContext(
            request, FileInteractionMode.View, new byte[] { 2 }, 1, "image/png");
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, first)
            .Add(component => component.Kind, FileObjectViewKind.Image));
        await cut.Find("img").TriggerEventAsync("onload", EventArgs.Empty);
        cut.WaitForAssertion(() => Assert.False(
            cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden")));

        var replacement = cut.InvokeAsync(() => cut.Render(parameters => parameters
            .Add(component => component.Context, second)
            .Add(component => component.Kind, FileObjectViewKind.Image)));
        await module.SecondApplyEntered.Task.WaitAsync(AsyncOperationTimeout);

        Assert.True(cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden"));
        Assert.Equal(
            [
                FileObjectUrlInterop.ApplyMethod,
                FileObjectUrlInterop.RevokeMethod,
                FileObjectUrlInterop.ApplyMethod
            ],
            module.Invocations.Select(invocation => invocation.Identifier));
        Assert.NotEqual(
            module.Invocations[0].Arguments?[0],
            module.Invocations[2].Arguments?[0]);

        module.ReleaseSecondApply();
        await replacement;
        Assert.True(cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden"));
        await cut.Find("img").TriggerEventAsync("onload", EventArgs.Empty);
        cut.WaitForAssertion(() => Assert.False(
            cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden")));
    }

    [Fact]
    public async Task PdfTarget_BecomesVisibleAfterBlobBindingAndUsesInertFallbackOnError()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var request = new FileInteractionRequest(
            new FileReference("test", "report.pdf"),
            "report.pdf",
            mediaType: "application/pdf");
        var context = new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            new byte[] { 1, 2, 3 },
            0,
            "application/pdf");
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Pdf));

        Assert.False(cut.Find("[data-testid='interaction-pdf-view']").HasAttribute("hidden"));

        await cut.Find("object").TriggerEventAsync("onerror", EventArgs.Empty);
        Assert.True(cut.Find("[data-testid='interaction-pdf-view']").HasAttribute("hidden"));
        Assert.Contains(
            "host-provided action",
            cut.Find("[data-testid='interaction-object-fallback']").TextContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserTarget_IsFullySandboxedAndBindsTheBlobToSrc()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        var apply = module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var request = new FileInteractionRequest(
            new FileReference("test", "movie.avi"),
            "movie.avi",
            mediaType: "video/x-msvideo");
        var context = new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            new byte[] { 1, 2, 3 },
            0,
            "video/x-msvideo");

        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Browser));

        var frame = cut.Find("iframe");
        Assert.Equal(string.Empty, frame.GetAttribute("sandbox"));
        Assert.Equal("no-referrer", frame.GetAttribute("referrerpolicy"));
        Assert.Equal("movie.avi", frame.GetAttribute("title"));
        var invocation = Assert.Single(apply.Invocations);
        Assert.Equal("src", Assert.IsType<string>(invocation.Arguments[3]));
    }

    [Theory]
    [InlineData(FileObjectViewKind.Image, "image/png", "interaction-image-view", "img")]
    [InlineData(FileObjectViewKind.Pdf, "application/pdf", "interaction-pdf-view", "object")]
    [InlineData(FileObjectViewKind.Browser, "image/svg+xml", "interaction-browser-view", "iframe")]
    public void DefaultTarget_RendersDirectlyInsideTheExistingSurface(
        FileObjectViewKind kind,
        string mediaType,
        string surfaceTestId,
        string targetSelector)
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var context = CreateContext("sample.bin", mediaType);

        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, kind));

        Assert.NotNull(cut.Find($"[data-testid='{surfaceTestId}'] > {targetSelector}"));
        Assert.Empty(cut.FindAll("[data-testid='target-frame']"));
    }

    [Fact]
    public async Task TargetFrame_WrapsImageTargetWithoutChangingLoadOrErrorHandling()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        var apply = module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var context = CreateContext("photo.png", "image/png");
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Image)
            .Add(component => component.TargetFrame, TargetFrame));

        var surface = cut.Find("[data-testid='interaction-image-view']");
        var frame = cut.Find("[data-testid='target-frame']");
        Assert.Equal("interaction-image-view", frame.ParentElement?.GetAttribute("data-testid"));
        Assert.NotNull(frame.QuerySelector(":scope > img"));
        Assert.Equal(nameof(FileObjectViewKind.Image), frame.GetAttribute("data-kind"));
        Assert.Equal("src", Assert.IsType<string>(Assert.Single(apply.Invocations).Arguments[3]));

        await cut.Find("img").TriggerEventAsync("onload", EventArgs.Empty);
        Assert.False(surface.HasAttribute("hidden"));

        await cut.Find("img").TriggerEventAsync("onerror", EventArgs.Empty);
        Assert.True(surface.HasAttribute("hidden"));
        Assert.Null(frame.QuerySelector("[data-testid='interaction-object-fallback']"));
        Assert.NotNull(cut.Find("[data-testid='interaction-object-fallback']"));
    }

    [Fact]
    public void TargetFrame_WrapsSvgSandboxWithoutReplacingItsSecurityBoundary()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        var apply = module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var context = CreateContext("hostile.svg", "image/svg+xml");
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Browser)
            .Add(component => component.TargetFrame, TargetFrame));

        var surface = cut.Find("[data-testid='interaction-browser-view']");
        var frame = cut.Find("[data-testid='target-frame']");
        var iframe = frame.QuerySelector(":scope > iframe");
        Assert.Equal("interaction-browser-view", frame.ParentElement?.GetAttribute("data-testid"));
        Assert.NotNull(iframe);
        Assert.Equal(nameof(FileObjectViewKind.Browser), frame.GetAttribute("data-kind"));
        Assert.Equal(string.Empty, iframe.GetAttribute("sandbox"));
        Assert.Equal("no-referrer", iframe.GetAttribute("referrerpolicy"));
        Assert.Empty(frame.QuerySelectorAll("img, object, svg"));
        var invocation = Assert.Single(apply.Invocations);
        Assert.Equal("src", Assert.IsType<string>(invocation.Arguments[3]));
        Assert.Equal(context.Content.ToArray(), Assert.IsType<byte[]>(invocation.Arguments[1]));
    }

    [Fact]
    public void TargetFrameReplacement_RebindsTheObjectUrlToTheNewTarget()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        var apply = module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var revoke = module.SetupVoid(FileObjectUrlInterop.RevokeMethod, _ => true).SetVoidResult();
        var context = CreateContext("photo.png", "image/png");
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Image)
            .Add(component => component.TargetFrame, CreateTargetFrame("first")));
        var firstTarget = Assert.IsType<ElementReference>(
            Assert.Single(apply.Invocations).Arguments[0]);

        cut.Render(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Image)
            .Add(component => component.TargetFrame, CreateTargetFrame("second")));

        Assert.Equal(2, apply.Invocations.Count);
        var secondTarget = Assert.IsType<ElementReference>(
            apply.Invocations.ElementAt(1).Arguments[0]);
        Assert.NotEqual(firstTarget.Id, secondTarget.Id);
        var revokedTarget = Assert.IsType<ElementReference>(
            Assert.Single(revoke.Invocations).Arguments[0]);
        Assert.Equal(firstTarget.Id, revokedTarget.Id);
        Assert.Equal("second", cut.Find("[data-testid='target-frame']").GetAttribute("data-frame-key"));
    }

    [Fact]
    public async Task ImageDecodeFailure_HidesTargetAndShowsInertFallback()
    {
        var module = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        module.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        var request = new FileInteractionRequest(
            new FileReference("test", "corrupt.png"),
            "corrupt.png",
            mediaType: "image/png");
        var context = new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            new byte[] { 1, 2, 3 },
            0,
            "image/png");
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.Kind, FileObjectViewKind.Image));

        await cut.Find("img").TriggerEventAsync("onerror", EventArgs.Empty);

        Assert.True(cut.Find("[data-testid='interaction-image-view']").HasAttribute("hidden"));
        Assert.Contains(
            "host-provided action",
            cut.Find("[data-testid='interaction-object-fallback']").TextContent,
            StringComparison.Ordinal);
    }

    private static FileInteractionRenderContext CreateContext(string fileName, string mediaType)
    {
        var request = new FileInteractionRequest(
            new FileReference("test", fileName),
            fileName,
            mediaType: mediaType);
        return new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            new byte[] { 1, 2, 3 },
            0,
            mediaType);
    }

    private static RenderFragment<FileObjectViewTargetFrameContext> TargetFrame
        => CreateTargetFrame("default");

    private static RenderFragment<FileObjectViewTargetFrameContext> CreateTargetFrame(string frameKey)
        => context => builder =>
        {
            builder.OpenElement(0, "div");
            builder.SetKey(frameKey);
            builder.AddAttribute(1, "data-testid", "target-frame");
            builder.AddAttribute(2, "data-kind", context.Kind.ToString());
            builder.AddAttribute(3, "data-frame-key", frameKey);
            builder.AddContent(4, context.TargetContent);
            builder.CloseElement();
        };

    private sealed class RecordingJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        public string? Identifier { get; private set; }
        public object?[]? Arguments { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Identifier = identifier;
            Arguments = args;
            return ValueTask.FromResult((TValue)module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InvokeAsync<TValue>(identifier, args);
        }
    }

    private sealed class RecordingJsObjectReference : IJSObjectReference
    {
        public List<(string Identifier, object?[]? Arguments)> Invocations { get; } = [];
        public bool WasDisposed { get; private set; }
        public Exception? RevokeError { get; init; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add((identifier, args));
            if (identifier == FileObjectUrlInterop.RevokeMethod && RevokeError is not null)
            {
                return ValueTask.FromException<TValue>(RevokeError);
            }

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InvokeAsync<TValue>(identifier, args);
        }

        public ValueTask DisposeAsync()
        {
            WasDisposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ControlledApplyJsObjectReference : IJSObjectReference
    {
        private readonly Queue<TaskCompletionSource> releases = [];
        private int concurrency;

        public TaskCompletionSource FirstApplyEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondApplyEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<object?[]> ApplyArguments { get; } = [];

        public List<string> InvocationIdentifiers { get; } = [];

        public int ApplyCount => ApplyArguments.Count;

        public int MaximumConcurrency { get; private set; }

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            InvocationIdentifiers.Add(identifier);
            if (identifier != FileObjectUrlInterop.ApplyMethod)
            {
                return default!;
            }

            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            releases.Enqueue(release);
            ApplyArguments.Add(args ?? []);
            concurrency++;
            MaximumConcurrency = Math.Max(MaximumConcurrency, concurrency);
            if (ApplyCount == 1)
            {
                FirstApplyEntered.TrySetResult();
            }
            else if (ApplyCount == 2)
            {
                SecondApplyEntered.TrySetResult();
            }

            try
            {
                await release.Task;
                return default!;
            }
            finally
            {
                concurrency--;
            }
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InvokeAsync<TValue>(identifier, args);
        }

        public void ReleaseNextApply()
        {
            Assert.NotEmpty(releases);
            releases.Dequeue().TrySetResult();
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DelayedReplacementJsObjectReference : IJSObjectReference
    {
        private readonly TaskCompletionSource releaseSecond =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int applyCount;

        public TaskCompletionSource SecondApplyEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<(string Identifier, object?[]? Arguments)> Invocations { get; } = [];

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add((identifier, args));
            if (identifier == FileObjectUrlInterop.ApplyMethod
                && Interlocked.Increment(ref applyCount) == 2)
            {
                SecondApplyEntered.TrySetResult();
                await releaseSecond.Task;
            }

            return default!;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InvokeAsync<TValue>(identifier, args);
        }

        public void ReleaseSecondApply() => releaseSecond.TrySetResult();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
