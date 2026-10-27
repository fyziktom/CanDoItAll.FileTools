using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>What an unmodified mouse wheel does over a <see cref="FileZoomPanFrame"/>.</summary>
public enum FileZoomPanWheelMode
{
    /// <summary>The wheel neither zooms nor pans; the surrounding page keeps scrolling.</summary>
    Disabled = 0,

    /// <summary>
    /// The wheel zooms around the pointer. When a zoom limit is reached the event is released so the page can scroll.
    /// Ctrl or Meta plus wheel always zooms and is never released to browser page zoom.
    /// </summary>
    Zoom = 1,

    /// <summary>
    /// Ctrl or Meta plus wheel zooms. An unmodified wheel pans overflowing content and is released to the page once
    /// the content cannot move further in that direction.
    /// </summary>
    ControlKey = 2
}

internal readonly record struct FileZoomPanFrameOptions(
    bool Enabled,
    bool SuppressContentInteraction,
    FileZoomPanWheelMode WheelMode,
    double MinimumZoom,
    double MaximumZoom,
    double ZoomFactor,
    double KeyboardPanStep)
{
    public static void Validate(
        FileZoomPanWheelMode wheelMode,
        double minimumZoom,
        double maximumZoom,
        double zoomFactor,
        double keyboardPanStep)
    {
        if (!Enum.IsDefined(wheelMode))
        {
            throw new ArgumentOutOfRangeException(nameof(wheelMode));
        }

        if (!double.IsFinite(minimumZoom) || minimumZoom <= 0 || minimumZoom > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumZoom),
                "Minimum zoom must be finite, greater than zero, and no greater than one.");
        }

        if (!double.IsFinite(maximumZoom) || maximumZoom < 1 || maximumZoom < minimumZoom)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumZoom),
                "Maximum zoom must be finite, at least one, and no less than minimum zoom.");
        }

        if (!double.IsFinite(zoomFactor) || zoomFactor <= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(zoomFactor),
                "Zoom factor must be finite and greater than one.");
        }

        if (!double.IsFinite(keyboardPanStep) || keyboardPanStep <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keyboardPanStep),
                "Keyboard pan step must be finite and greater than zero.");
        }
    }

    public object ToInterop()
        => new
        {
            enabled = Enabled,
            suppressContentInteraction = SuppressContentInteraction,
            wheelMode = (int)WheelMode,
            minimumZoom = MinimumZoom,
            maximumZoom = MaximumZoom,
            zoomFactor = ZoomFactor,
            keyboardPanStep = KeyboardPanStep
        };
}

/// <summary>Owns the collocated zoom/pan module for one frame instance and tolerates circuit teardown.</summary>
internal sealed class FileZoomPanFrameInterop
{
    internal const string ModulePath =
        "./_content/CanDoItAll.FileTools.FileInteraction.Components/Components/FileZoomPanFrame.razor.js";
    internal const string ImportMethod = "import";
    internal const string InitializeMethod = "initialize";
    internal const string ConfigureMethod = "configure";
    internal const string ResetMethod = "reset";
    internal const string ZoomByMethod = "zoomBy";
    internal const string DestroyMethod = "destroy";

    private readonly IJSRuntime js;
    private readonly SemaphoreSlim gate = new(1, 1);
    private IJSObjectReference? module;
    private bool initialized;
    private bool disposed;

    public FileZoomPanFrameInterop(IJSRuntime js)
    {
        this.js = js ?? throw new ArgumentNullException(nameof(js));
    }

    public async ValueTask InitializeAsync(ElementReference viewport, ElementReference content, object options)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            module ??= await js.InvokeAsync<IJSObjectReference>(ImportMethod, ModulePath);
            await module.InvokeVoidAsync(InitializeMethod, viewport, content, options);
            initialized = true;
        }
        finally
        {
            gate.Release();
        }
    }

    public ValueTask ConfigureAsync(ElementReference viewport, object options)
        => InvokeAsync(ConfigureMethod, viewport, options);

    public ValueTask ResetAsync(ElementReference viewport)
        => InvokeAsync(ResetMethod, viewport);

    public ValueTask ZoomByAsync(ElementReference viewport, double factor)
        => InvokeAsync(ZoomByMethod, viewport, factor);

    public async ValueTask DisposeAsync(ElementReference viewport)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var activeModule = module;
            module = null;
            if (activeModule is null)
            {
                return;
            }

            if (initialized)
            {
                try
                {
                    await activeModule.InvokeVoidAsync(DestroyMethod, viewport);
                }
                catch (Exception exception) when (IsExpectedFailure(exception))
                {
                }
            }

            try
            {
                await activeModule.DisposeAsync();
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async ValueTask InvokeAsync(string identifier, params object?[] arguments)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed || module is null || !initialized)
            {
                return;
            }

            await module.InvokeVoidAsync(identifier, arguments);
        }
        finally
        {
            gate.Release();
        }
    }

    internal static bool IsExpectedFailure(Exception exception)
        => exception is JSDisconnectedException
            or JSException
            or ObjectDisposedException
            or OperationCanceledException
            or InvalidOperationException;
}
