namespace CanDoItAll.FileTools.FileBrowser.Tests;

internal sealed class FakeFileBrowserProvider : IFileBrowserProvider, IFileBrowserSearchProvider
{
    public FakeFileBrowserProvider(FileBrowserSourceDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    public FileBrowserSourceDescriptor Descriptor { get; }

    public Func<FileBrowserMetadataRequest, CancellationToken, ValueTask<FileBrowserItem>>? RootHandler { get; set; }

    public Func<FileBrowserItemKey, FileBrowserMetadataRequest, CancellationToken, ValueTask<IReadOnlyList<FileBrowserItem>>>? PathHandler { get; set; }

    public Func<FileBrowserBrowseRequest, CancellationToken, ValueTask<FileBrowserPage>>? BrowseHandler { get; set; }

    public Func<FileBrowserSearchRequest, CancellationToken, ValueTask<FileBrowserSearchPage>>? SearchHandler { get; set; }

    public int RootCallCount { get; private set; }

    public int PathCallCount { get; private set; }

    public int BrowseCallCount { get; private set; }

    public int SearchCallCount { get; private set; }

    public List<FileBrowserItemKey> PathCalls { get; } = [];

    public List<FileBrowserBrowseRequest> BrowseCalls { get; } = [];

    public List<FileBrowserSearchRequest> SearchCalls { get; } = [];

    public ValueTask<FileBrowserItem> GetRootAsync(
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        RootCallCount++;
        return RootHandler?.Invoke(metadata, cancellationToken)
            ?? ValueTask.FromException<FileBrowserItem>(Unexpected(nameof(GetRootAsync)));
    }

    public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
        FileBrowserItemKey itemKey,
        FileBrowserMetadataRequest metadata,
        CancellationToken cancellationToken = default)
    {
        PathCallCount++;
        PathCalls.Add(itemKey);
        return PathHandler?.Invoke(itemKey, metadata, cancellationToken)
            ?? ValueTask.FromException<IReadOnlyList<FileBrowserItem>>(Unexpected(nameof(GetPathAsync)));
    }

    public ValueTask<FileBrowserPage> BrowseAsync(
        FileBrowserBrowseRequest request,
        CancellationToken cancellationToken = default)
    {
        BrowseCallCount++;
        BrowseCalls.Add(request);
        return BrowseHandler?.Invoke(request, cancellationToken)
            ?? ValueTask.FromException<FileBrowserPage>(Unexpected(nameof(BrowseAsync)));
    }

    public ValueTask<FileBrowserSearchPage> SearchAsync(
        FileBrowserSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        SearchCallCount++;
        SearchCalls.Add(request);
        return SearchHandler?.Invoke(request, cancellationToken)
            ?? ValueTask.FromException<FileBrowserSearchPage>(Unexpected(nameof(SearchAsync)));
    }

    private static InvalidOperationException Unexpected(string operation)
        => new($"Unexpected provider operation: {operation}.");
}

