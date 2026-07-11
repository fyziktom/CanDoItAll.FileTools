using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileObjectUrlInteropTests : BunitContext
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
        await module.FirstApplyEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = interop.ApplyAsync(default, new byte[] { 2 }, "image/png", "src").AsTask();

        Assert.Equal(1, module.ApplyCount);
        module.ReleaseNextApply();
        await module.SecondApplyEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

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
        await module.SecondApplyEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

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
    public async Task PdfTarget_RemainsHiddenUntilBrowserLoadAndUsesInertFallbackOnError()
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

        Assert.True(cut.Find("[data-testid='interaction-pdf-view']").HasAttribute("hidden"));
        await cut.Find("object").TriggerEventAsync("onload", EventArgs.Empty);
        Assert.False(cut.Find("[data-testid='interaction-pdf-view']").HasAttribute("hidden"));

        await cut.Find("object").TriggerEventAsync("onerror", EventArgs.Empty);
        Assert.True(cut.Find("[data-testid='interaction-pdf-view']").HasAttribute("hidden"));
        Assert.Contains(
            "host-provided action",
            cut.Find("[data-testid='interaction-object-fallback']").TextContent,
            StringComparison.Ordinal);
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
