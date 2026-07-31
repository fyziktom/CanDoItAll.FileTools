using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CanDoItAll.FileTools.FileInteraction.Components;

public enum FileObjectViewKind
{
    /// <summary>Displays bounded raster image bytes.</summary>
    Image,
    /// <summary>
    /// Uses the browser's native PDF object viewer. Links and document actions inside that viewer remain
    /// browser-owned and are not intercepted or authorized by FileInteraction.
    /// </summary>
    Pdf,
    /// <summary>
    /// Gives the browser a bounded blob URL inside a fully sandboxed frame. Rendering support remains
    /// browser- and codec-dependent, while file content stays isolated from the host document.
    /// </summary>
    Browser
}

public sealed class FileObjectViewTargetFrameContext
{
    internal FileObjectViewTargetFrameContext(
        FileObjectViewKind kind,
        RenderFragment targetContent)
    {
        Kind = kind;
        TargetContent = targetContent ?? throw new ArgumentNullException(nameof(targetContent));
    }

    public FileObjectViewKind Kind { get; }

    public RenderFragment TargetContent { get; }
}

internal readonly record struct FileObjectContentStamp(
    FileReference File,
    long EditRevision,
    int Length,
    ReadOnlyMemory<byte> Content,
    FileContentRevision? ContentRevision,
    string MediaType,
    FileObjectViewKind Kind);

internal sealed class FileObjectUrlInterop
{
    internal const string ModulePath =
        "./_content/CanDoItAll.FileTools.FileInteraction.Components/Components/FileObjectView.razor.js";
    internal const string ImportMethod = "import";
    internal const string ApplyMethod = "applyObjectUrl";
    internal const string RevokeMethod = "revokeObjectUrl";

    private readonly IJSRuntime js;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private IJSObjectReference? module;
    private long latestOperation;
    private bool disposed;

    public FileObjectUrlInterop(IJSRuntime js)
    {
        this.js = js ?? throw new ArgumentNullException(nameof(js));
    }

    public async ValueTask<bool> ApplyAsync(
        ElementReference target,
        ReadOnlyMemory<byte> content,
        string mediaType,
        string attributeName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var operation = Interlocked.Increment(ref latestOperation);
        await operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed || operation != Volatile.Read(ref latestOperation))
            {
                return false;
            }

            module ??= await js.InvokeAsync<IJSObjectReference>(ImportMethod, ModulePath);
            if (operation != Volatile.Read(ref latestOperation))
            {
                return false;
            }

            await module.InvokeVoidAsync(
                ApplyMethod,
                target,
                content.ToArray(),
                mediaType,
                attributeName);
            if (operation == Volatile.Read(ref latestOperation))
            {
                return true;
            }

            try
            {
                await module.InvokeVoidAsync(RevokeMethod, target);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
            }

            return false;
        }
        catch (Exception exception) when (
            operation != Volatile.Read(ref latestOperation) && IsExpectedFailure(exception))
        {
            return false;
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async ValueTask DisposeAsync(ElementReference target)
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Interlocked.Increment(ref latestOperation);
        await operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var activeModule = module;
            module = null;
            if (activeModule is null)
            {
                return;
            }

            try
            {
                await activeModule.InvokeVoidAsync(RevokeMethod, target);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
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
            operationGate.Release();
        }
    }

    public async ValueTask RevokeAsync(ElementReference target)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var operation = Interlocked.Increment(ref latestOperation);
        await operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed || operation != Volatile.Read(ref latestOperation) || module is null)
            {
                return;
            }

            try
            {
                await module.InvokeVoidAsync(RevokeMethod, target);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    internal static bool IsExpectedFailure(Exception exception)
        => exception is JSDisconnectedException
            or JSException
            or ObjectDisposedException
            or OperationCanceledException;
}
