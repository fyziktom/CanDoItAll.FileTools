using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileZoomPanFrameTests : FileToolsBunitContext
{
    [Fact]
    public void Render_ExposesToolbarViewportAndContentPlane()
    {
        var module = SetupModule();
        var initialize = module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();

        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.AriaLabel, " Zoom photo ")
            .AddChildContent("<img alt=\"photo\" data-testid=\"child\" />"));

        var toolbar = cut.Find("[role='toolbar']");
        Assert.Equal(3, toolbar.QuerySelectorAll("button").Length);
        Assert.Equal("100%", cut.Find("[data-testid='interaction-zoom-readout']").TextContent);
        var viewport = cut.Find("[data-testid='interaction-zoom-pan-viewport']");
        Assert.Equal("0", viewport.GetAttribute("tabindex"));
        Assert.Equal("group", viewport.GetAttribute("role"));
        Assert.Equal("Zoom photo", viewport.GetAttribute("aria-label"));
        var content = cut.Find("[data-testid='interaction-zoom-pan-content']");
        Assert.False(content.HasAttribute("inert"));
        Assert.NotNull(content.QuerySelector(":scope > img[data-testid='child']"));
        Assert.Contains("cdi-ft-zoom-pan--controls", cut.Find("[data-testid='interaction-zoom-pan']").ClassName);

        var invocation = Assert.Single(initialize.Invocations);
        Assert.IsType<ElementReference>(invocation.Arguments[0]);
        Assert.IsType<ElementReference>(invocation.Arguments[1]);
        Assert.Equal(1, ReadOption(invocation.Arguments[2], "wheelMode"));
        Assert.Equal(0.25d, ReadOption(invocation.Arguments[2], "minimumZoom"));
        Assert.Equal(8d, ReadOption(invocation.Arguments[2], "maximumZoom"));
        Assert.Equal(true, ReadOption(invocation.Arguments[2], "enabled"));
    }

    [Fact]
    public void Render_DisabledFrameHidesToolbarAndKeyboardFocus()
    {
        var module = SetupModule();
        module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();

        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.Enabled, false)
            .AddChildContent("<div>content</div>"));

        Assert.Empty(cut.FindAll("[role='toolbar']"));
        Assert.False(cut.Find("[data-testid='interaction-zoom-pan-viewport']").HasAttribute("tabindex"));
        Assert.Equal("Zoomable and pannable content",
            cut.Find("[data-testid='interaction-zoom-pan-viewport']").GetAttribute("aria-label"));
    }

    [Fact]
    public void Render_WithoutControlsKeepsViewportInteractive()
    {
        var module = SetupModule();
        module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();

        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.ShowControls, false)
            .AddChildContent("<div>content</div>"));

        Assert.Empty(cut.FindAll("[role='toolbar']"));
        Assert.Equal("0", cut.Find("[data-testid='interaction-zoom-pan-viewport']").GetAttribute("tabindex"));
    }

    [Fact]
    public void SuppressContentInteraction_MakesTheContentPlaneInert()
    {
        var module = SetupModule();
        var initialize = module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();

        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.SuppressContentInteraction, true)
            .AddChildContent("<iframe title=\"frame\"></iframe>"));

        var content = cut.Find("[data-testid='interaction-zoom-pan-content']");
        Assert.True(content.HasAttribute("inert"));
        Assert.NotNull(cut.Find("iframe").Closest("[inert]"));
        Assert.Contains("cdi-ft-zoom-pan--suppressed", cut.Find("[data-testid='interaction-zoom-pan']").ClassName);
        Assert.Equal(true, ReadOption(Assert.Single(initialize.Invocations).Arguments[2], "suppressContentInteraction"));
    }

    [Theory]
    [InlineData(nameof(FileZoomPanFrame.MinimumZoom), 0d)]
    [InlineData(nameof(FileZoomPanFrame.MinimumZoom), 1.5d)]
    [InlineData(nameof(FileZoomPanFrame.MaximumZoom), 0.5d)]
    [InlineData(nameof(FileZoomPanFrame.ZoomFactor), 1d)]
    [InlineData(nameof(FileZoomPanFrame.KeyboardPanStep), 0d)]
    [InlineData(nameof(FileZoomPanFrame.KeyboardPanStep), double.NaN)]
    public void InvalidNumericParameters_FailFast(string parameterName, double value)
    {
        SetupModule().SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();

        Assert.Throws<ArgumentOutOfRangeException>(() => Render<FileZoomPanFrame>(parameters =>
        {
            switch (parameterName)
            {
                case nameof(FileZoomPanFrame.MinimumZoom):
                    parameters.Add(component => component.MinimumZoom, value);
                    break;
                case nameof(FileZoomPanFrame.MaximumZoom):
                    parameters.Add(component => component.MaximumZoom, value);
                    break;
                case nameof(FileZoomPanFrame.ZoomFactor):
                    parameters.Add(component => component.ZoomFactor, value);
                    break;
                case nameof(FileZoomPanFrame.KeyboardPanStep):
                    parameters.Add(component => component.KeyboardPanStep, value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(parameterName));
            }

            parameters.AddChildContent("<div>content</div>");
        }));
    }

    [Fact]
    public void UndefinedWheelMode_FailsFast()
    {
        SetupModule().SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();

        Assert.Throws<ArgumentOutOfRangeException>(() => Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.WheelMode, (FileZoomPanWheelMode)42)
            .AddChildContent("<div>content</div>")));
    }

    [Fact]
    public async Task ToolbarButtons_DriveTheCollocatedModule()
    {
        var module = SetupModule();
        module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();
        var zoomBy = module.SetupVoid(FileZoomPanFrameInterop.ZoomByMethod, _ => true).SetVoidResult();
        var reset = module.SetupVoid(FileZoomPanFrameInterop.ResetMethod, _ => true).SetVoidResult();
        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.ZoomFactor, 1.5)
            .AddChildContent("<div>content</div>"));

        await cut.Find("[data-testid='interaction-zoom-in']").ClickAsync();
        await cut.Find("[data-testid='interaction-zoom-out']").ClickAsync();
        await cut.Find("[data-testid='interaction-zoom-reset']").ClickAsync();

        Assert.Equal(2, zoomBy.Invocations.Count);
        Assert.Equal(1.5d, Assert.IsType<double>(zoomBy.Invocations.ElementAt(0).Arguments[1]));
        Assert.Equal(1d / 1.5d, Assert.IsType<double>(zoomBy.Invocations.ElementAt(1).Arguments[1]));
        Assert.Single(reset.Invocations);
    }

    [Fact]
    public async Task PublicMethods_AreNoOpsWhileDisabled()
    {
        var module = SetupModule();
        module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();
        var zoomBy = module.SetupVoid(FileZoomPanFrameInterop.ZoomByMethod, _ => true).SetVoidResult();
        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.Enabled, false)
            .AddChildContent("<div>content</div>"));

        await cut.InvokeAsync(() => cut.Instance.ZoomInAsync());

        Assert.Empty(zoomBy.Invocations);
    }

    [Fact]
    public void ResetKeyChange_ResetsOnlyWhenTheValueDiffers()
    {
        var module = SetupModule();
        module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();
        var reset = module.SetupVoid(FileZoomPanFrameInterop.ResetMethod, _ => true).SetVoidResult();
        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .Add(component => component.ResetKey, new FileReference("test", "first"))
            .AddChildContent("<div>content</div>"));

        cut.Render(parameters => parameters
            .Add(component => component.ResetKey, new FileReference("test", "first"))
            .AddChildContent("<div>content</div>"));
        Assert.Empty(reset.Invocations);

        cut.Render(parameters => parameters
            .Add(component => component.ResetKey, new FileReference("test", "second"))
            .AddChildContent("<div>content</div>"));
        Assert.Single(reset.Invocations);
    }

    [Fact]
    public void OptionChange_ReconfiguresTheModuleOnce()
    {
        var module = SetupModule();
        module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();
        var configure = module.SetupVoid(FileZoomPanFrameInterop.ConfigureMethod, _ => true).SetVoidResult();
        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .AddChildContent("<div>content</div>"));

        cut.Render(parameters => parameters
            .Add(component => component.WheelMode, FileZoomPanWheelMode.ControlKey)
            .AddChildContent("<div>content</div>"));
        cut.Render(parameters => parameters
            .Add(component => component.WheelMode, FileZoomPanWheelMode.ControlKey)
            .AddChildContent("<div>content</div>"));

        var invocation = Assert.Single(configure.Invocations);
        Assert.Equal(2, ReadOption(invocation.Arguments[1], "wheelMode"));
    }

    [Fact]
    public async Task Dispose_DestroysTheFrameStateOnce()
    {
        var module = SetupModule();
        module.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();
        var destroy = module.SetupVoid(FileZoomPanFrameInterop.DestroyMethod, _ => true).SetVoidResult();
        var cut = Render<FileZoomPanFrame>(parameters => parameters.AddChildContent("<div>content</div>"));

        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());
        await cut.InvokeAsync(() => cut.Instance.DisposeAsync().AsTask());

        Assert.Single(destroy.Invocations);
    }

    [Fact]
    public async Task ModuleImportFailure_KeepsTheStaticFrameRenderedAndDisablesCommands()
    {
        var runtime = new ThrowingJsRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);

        var cut = Render<FileZoomPanFrame>(parameters => parameters
            .AddChildContent("<img alt=\"photo\" data-testid=\"child\" />"));
        await cut.InvokeAsync(() => cut.Instance.ZoomInAsync());

        Assert.NotNull(cut.Find("[data-testid='child']"));
        Assert.NotNull(cut.Find("[role='toolbar']"));
        Assert.Equal(1, runtime.ImportAttempts);
    }

    [Fact]
    public async Task Interop_IgnoresCommandsBeforeInitializationAndDisposesOnce()
    {
        var module = new RecordingJsObjectReference();
        var interop = new FileZoomPanFrameInterop(new RecordingJsRuntime(module));

        await interop.ZoomByAsync(default, 2);
        await interop.InitializeAsync(default, default, new { enabled = true });
        await interop.ZoomByAsync(default, 2);
        await interop.DisposeAsync(default);
        await interop.DisposeAsync(default);

        Assert.Equal(
            [
                FileZoomPanFrameInterop.InitializeMethod,
                FileZoomPanFrameInterop.ZoomByMethod,
                FileZoomPanFrameInterop.DestroyMethod
            ],
            module.Invocations.Select(invocation => invocation.Identifier));
        Assert.True(module.WasDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => interop.ZoomByAsync(default, 2).AsTask());
    }

    [Fact]
    public async Task Interop_WhenDestroyFailsStillDisposesTheModule()
    {
        var module = new RecordingJsObjectReference
        {
            DestroyError = new JSDisconnectedException("Circuit disconnected.")
        };
        var interop = new FileZoomPanFrameInterop(new RecordingJsRuntime(module));
        await interop.InitializeAsync(default, default, new { enabled = true });

        await interop.DisposeAsync(default);

        Assert.True(module.WasDisposed);
    }

    private Bunit.BunitJSModuleInterop SetupModule()
        => JSInterop.SetupModule(FileZoomPanFrameInterop.ModulePath);

    private static object? ReadOption(object? options, string name)
    {
        Assert.NotNull(options);
        var property = options.GetType().GetProperty(name);
        Assert.NotNull(property);
        return property.GetValue(options);
    }

    private sealed class ThrowingJsRuntime : IJSRuntime
    {
        public int ImportAttempts { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            ImportAttempts++;
            return ValueTask.FromException<TValue>(new JSException("The module is unavailable."));
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class RecordingJsRuntime(IJSObjectReference module) : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult((TValue)module);

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

        public Exception? DestroyError { get; init; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Invocations.Add((identifier, args));
            if (identifier == FileZoomPanFrameInterop.DestroyMethod && DestroyError is not null)
            {
                return ValueTask.FromException<TValue>(DestroyError);
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
}
