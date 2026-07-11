namespace CanDoItAll.FileTools.FileBrowser;

/// <summary>Projects capability-driven built-in actions consistently for every renderer.</summary>
public static class FileBrowserBuiltInActions
{
    public static IReadOnlyList<FileBrowserActionDescriptor> GetFor(FileBrowserItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var actions = new List<FileBrowserActionDescriptor>();
        if (item.IsContainer
                ? item.Supports(FileBrowserItemCapabilities.Navigate)
                    || item.Supports(FileBrowserItemCapabilities.Open)
                : item.Supports(FileBrowserItemCapabilities.Open))
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileBrowserActionIds.Open,
                item.IsContainer ? "Open folder" : "Open",
                "open_in_new",
                isPrimary: true));
        }

        if (item.Supports(FileBrowserItemCapabilities.OpenInNewTab))
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileBrowserActionIds.OpenInNewTab,
                "Open in new tab",
                "open_in_new"));
        }

        if (item.Supports(FileBrowserItemCapabilities.CopyPath) && item.DisplayPath is not null)
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileBrowserActionIds.CopyPath,
                "Copy path",
                "content_copy"));
        }

        if (item.Supports(FileBrowserItemCapabilities.CopyContentIdentity)
            && item.ContentIdentity is not null)
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileBrowserActionIds.CopyContentIdentity,
                "Copy content ID",
                "fingerprint"));
        }

        if (item.Supports(FileBrowserItemCapabilities.DownloadFile)
            || item.Supports(FileBrowserItemCapabilities.DownloadDirectory))
        {
            actions.Add(new FileBrowserActionDescriptor(
                FileBrowserActionIds.Download,
                "Download",
                "download"));
        }

        return actions;
    }
}

/// <summary>Result of resolving an action before a session commits optional navigation.</summary>
public sealed record FileBrowserActionDispatch(
    FileBrowserItemKey? NavigationKey,
    FileBrowserActionResult? Result)
{
    public static FileBrowserActionDispatch Navigate(FileBrowserItemKey itemKey)
        => new(itemKey, null);

    public static FileBrowserActionDispatch Complete(FileBrowserActionResult result)
        => new(null, result ?? throw new ArgumentNullException(nameof(result)));
}

/// <summary>Owns capability validation and optional provider action/content delegation.</summary>
public sealed class FileBrowserActionDispatcher
{
    public async ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetActionsAsync(
        IFileBrowserProvider provider,
        FileBrowserItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(item);
        var actions = new List<FileBrowserActionDescriptor>(FileBrowserBuiltInActions.GetFor(item));
        if (provider is not IFileBrowserActionProvider actionProvider
            || !provider.Descriptor.Supports(FileBrowserSourceCapabilities.CustomActions)
            || !item.Supports(FileBrowserItemCapabilities.CustomActions))
        {
            return actions;
        }

        IReadOnlyList<FileBrowserActionDescriptor> customActions =
            await actionProvider.GetActionsAsync(item.Key, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (customActions is null || customActions.Any(action => action is null))
        {
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider returned an invalid custom action list.");
        }

        HashSet<string> actionIds = actions.Select(action => action.Id).ToHashSet(StringComparer.Ordinal);
        foreach (FileBrowserActionDescriptor customAction in customActions)
        {
            if (FileBrowserActionIds.IsReserved(customAction.Id)
                && !actionIds.Contains(customAction.Id))
            {
                throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                    $"The provider returned reserved action '{customAction.Id}' for an item that does not support it.");
            }

            if (actionIds.Add(customAction.Id))
            {
                actions.Add(customAction);
            }
        }

        return actions;
    }

    public async ValueTask<FileBrowserActionDispatch> DispatchAsync(
        IFileBrowserProvider provider,
        FileBrowserItem item,
        FileBrowserActionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(request);
        switch (request.ActionId)
        {
            case FileBrowserActionIds.Open
                when item.IsContainer && item.Supports(FileBrowserItemCapabilities.Navigate):
                return FileBrowserActionDispatch.Navigate(item.Key);
            case FileBrowserActionIds.Open
                when item.Supports(FileBrowserItemCapabilities.Open) && item.OpenUri is not null:
            case FileBrowserActionIds.OpenInNewTab
                when item.Supports(FileBrowserItemCapabilities.OpenInNewTab) && item.OpenUri is not null:
                return FileBrowserActionDispatch.Complete(
                    FileBrowserActionResult.Success(navigationUri: item.OpenUri));
            case FileBrowserActionIds.CopyPath
                when item.Supports(FileBrowserItemCapabilities.CopyPath) && item.DisplayPath is not null:
                return FileBrowserActionDispatch.Complete(
                    FileBrowserActionResult.Success(value: item.DisplayPath));
            case FileBrowserActionIds.CopyContentIdentity
                when item.Supports(FileBrowserItemCapabilities.CopyContentIdentity)
                    && item.ContentIdentity is not null:
                return FileBrowserActionDispatch.Complete(
                    FileBrowserActionResult.Success(value: item.ContentIdentity.Value));
            case FileBrowserActionIds.Download
                when (item.Supports(FileBrowserItemCapabilities.DownloadFile)
                    || item.Supports(FileBrowserItemCapabilities.DownloadDirectory))
                    && item.DownloadUri is not null:
                return FileBrowserActionDispatch.Complete(
                    FileBrowserActionResult.Success(navigationUri: item.DownloadUri));
        }

        bool advertisedBuiltIn = FileBrowserBuiltInActions.GetFor(item)
            .Any(action => string.Equals(action.Id, request.ActionId, StringComparison.Ordinal));
        bool advertisedCustom = !FileBrowserActionIds.IsReserved(request.ActionId)
            && provider.Descriptor.Supports(FileBrowserSourceCapabilities.CustomActions)
            && item.Supports(FileBrowserItemCapabilities.CustomActions);
        if (provider is IFileBrowserActionProvider actionProvider
            && (advertisedBuiltIn || advertisedCustom))
        {
            FileBrowserActionResult result = await actionProvider.ExecuteAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return FileBrowserActionDispatch.Complete(result);
        }

        return FileBrowserActionDispatch.Complete(FileBrowserActionResult.Failure(new FileBrowserError(
            FileBrowserErrorCode.Unsupported,
            $"The action '{request.ActionId}' is not supported for this item.")));
    }

    public async ValueTask<FileBrowserContentLease> OpenReadAsync(
        IFileBrowserProvider provider,
        FileBrowserReadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);
        if (!provider.Descriptor.Supports(FileBrowserSourceCapabilities.ContentRead)
            || provider is not IFileBrowserContentProvider contentProvider)
        {
            throw Unsupported("This source does not provide file content reads.");
        }

        if ((request.Offset > 0 || request.Length.HasValue)
            && !provider.Descriptor.Supports(FileBrowserSourceCapabilities.RangeRead))
        {
            throw Unsupported("This source does not provide ranged file content reads.");
        }

        FileBrowserContentLease lease = await contentProvider.OpenReadAsync(request, cancellationToken);
        if (lease is null)
        {
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider returned no content lease.");
        }

        if (!lease.Stream.CanRead)
        {
            await lease.DisposeAsync();
            throw FileBrowserProviderResponseValidator.CorruptProviderResponse(
                "The provider returned a content stream that cannot be read.");
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await lease.DisposeAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return lease;
    }

    private static FileBrowserProviderException Unsupported(string message)
        => new(new FileBrowserError(FileBrowserErrorCode.Unsupported, message));
}

internal static class FileBrowserActionExecution
{
    public static async ValueTask<FileBrowserActionResult> RunAsync(
        Func<ValueTask<FileBrowserExecutedAction>> action,
        Action publishNavigation)
    {
        try
        {
            FileBrowserExecutedAction executed = await action();
            if (executed.NavigationCommitted)
            {
                publishNavigation();
            }

            return executed.Result;
        }
        catch (FileBrowserProviderException exception)
        {
            return FileBrowserActionResult.Failure(exception.Error);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and not ObjectDisposedException)
        {
            return FileBrowserActionResult.Failure(new FileBrowserError(
                FileBrowserErrorCode.ProviderFailure,
                "The source could not complete the file browser action.",
                isRetryable: true,
                technicalDetail: exception.ToString()));
        }
    }
}
