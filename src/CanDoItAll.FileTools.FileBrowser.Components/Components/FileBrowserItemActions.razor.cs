using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileBrowser.Components;

public partial class FileBrowserItemActions : ComponentBase, IAsyncDisposable
{
    private static long nextMenuId;
    private readonly CancellationTokenSource lifetime = new();
    private readonly string menuId = $"ft-file-actions-{Interlocked.Increment(ref nextMenuId)}";
    private CancellationTokenSource? loadCancellation;
    private IFileBrowserSession? previousSession;
    private FileBrowserItem? previousItem;
    private FileBrowserSourceDescriptor? previousSource;
    private FileBrowserItemKey previousItemKey;
    private long previousSnapshotRevision = long.MinValue;
    private long loadedSnapshotRevision = long.MinValue;
    private IReadOnlyList<FileBrowserActionDescriptor> actions = [];
    private string? error;
    private bool loading;
    private long loadVersion;
    private bool disposed;

    [Parameter, EditorRequired]
    public IFileBrowserSession Session { get; set; } = default!;

    [Parameter, EditorRequired]
    public FileBrowserItem Item { get; set; } = default!;

    [Parameter]
    public FileBrowserSourceDescriptor? Source { get; set; }

    [Parameter]
    public long SnapshotRevision { get; set; }

    [Parameter]
    public EventCallback<FileBrowserItemActionEventArgs> ActionRequested { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    protected override void OnParametersSet()
    {
        if (Session is null || Item is null)
        {
            throw new InvalidOperationException("FileBrowserItemActions requires a session and item.");
        }

        if (!ReferenceEquals(previousSession, Session)
            || previousItemKey != Item.Key
            || previousSnapshotRevision != SnapshotRevision
            || previousItem != Item
            || previousSource != Source)
        {
            loadCancellation?.Cancel();
            loadVersion++;
            previousSession = Session;
            previousItem = Item;
            previousSource = Source;
            previousItemKey = Item.Key;
            previousSnapshotRevision = SnapshotRevision;
            loadedSnapshotRevision = long.MinValue;
            loading = false;
            actions = [];
            error = null;
        }
    }

    private Task EnsureActionsAsync()
        => loading || actions.Count > 0
            ? Task.CompletedTask
            : LoadActionsAsync();

    private async Task LoadActionsAsync()
    {
        loadCancellation?.Cancel();
        var currentCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        loadCancellation = currentCancellation;
        IFileBrowserSession requestedSession = Session;
        FileBrowserItem requestedItem = Item;
        FileBrowserSourceDescriptor? requestedSource = Source;
        long requestedSnapshotRevision = SnapshotRevision;
        long version = ++loadVersion;
        loading = true;
        error = null;
        try
        {
            IReadOnlyList<FileBrowserActionDescriptor> loaded =
                await requestedSession.GetActionsAsync(requestedItem.Key, currentCancellation.Token);
            if (IsCurrentLoad(
                requestedSession,
                requestedItem,
                requestedSource,
                requestedSnapshotRevision,
                version))
            {
                actions = loaded;
                loadedSnapshotRevision = requestedSnapshotRevision;
            }
        }
        catch (OperationCanceledException) when (currentCancellation.IsCancellationRequested)
        {
            // A closed menu, changed item, or disposed component superseded the load.
        }
        catch (Exception) when (disposed || version != loadVersion)
        {
            // A later item or load superseded this failure.
        }
        catch (Exception) when (!disposed && version == loadVersion)
        {
            actions = [];
            error = "Actions could not be loaded.";
        }
        finally
        {
            if (version == loadVersion)
            {
                loading = false;
            }

            if (ReferenceEquals(loadCancellation, currentCancellation))
            {
                loadCancellation = null;
            }

            currentCancellation.Dispose();
        }
    }

    private async Task RequestActionAsync(FileBrowserActionDescriptor action)
    {
        if (Disabled
            || loadedSnapshotRevision != SnapshotRevision
            || !actions.Contains(action)
            || !FileBrowserInteractionPolicy.IsActionSupported(Item, Source, action.Id))
        {
            return;
        }

        await ActionRequested.InvokeAsync(new FileBrowserItemActionEventArgs(Item, action));
    }

    private bool IsCurrentLoad(
        IFileBrowserSession requestedSession,
        FileBrowserItem requestedItem,
        FileBrowserSourceDescriptor? requestedSource,
        long requestedSnapshotRevision,
        long version)
        => !disposed
            && !Disabled
            && version == loadVersion
            && ReferenceEquals(requestedSession, Session)
            && requestedItem == Item
            && requestedSource == Source
            && requestedSnapshotRevision == SnapshotRevision;

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        loadVersion++;
        if (loadCancellation is not null)
        {
            await loadCancellation.CancelAsync();
        }

        await lifetime.CancelAsync();
        lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
