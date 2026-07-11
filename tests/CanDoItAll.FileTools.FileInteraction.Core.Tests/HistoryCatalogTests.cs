namespace CanDoItAll.FileTools.FileInteraction.Core.Tests;

public sealed class HistoryCatalogTests
{
    [Fact]
    public async Task CreateAsync_NoFactoryMatches_ReturnsNull()
    {
        var catalog = new FileEditHistoryProviderCatalog([]);

        var history = await catalog.CreateAsync(Profile(history: false), Request());

        Assert.Null(history);
    }

    [Fact]
    public async Task CreateAsync_BoundedFactoryMatches_ReturnsIndependentProvider()
    {
        var catalog = new FileEditHistoryProviderCatalog([new BoundedTextHistoryProviderFactory()]);

        await using var history = await catalog.CreateAsync(Profile(history: true), Request());

        Assert.IsType<BoundedTextHistoryProvider>(history);
    }

    [Fact]
    public async Task CreateAsync_MultipleFactoriesMatch_ThrowsExplicitAmbiguity()
    {
        var catalog = new FileEditHistoryProviderCatalog([new AlwaysFactory(), new AlwaysFactory()]);

        var exception = await Assert.ThrowsAsync<FileEditHistoryFactoryAmbiguityException>(
            () => catalog.CreateAsync(Profile(history: true), Request()).AsTask());

        Assert.Equal(2, exception.MatchingFactoryCount);
        Assert.Equal(0, exception.Priority);
    }

    [Fact]
    public async Task CreateAsync_SpecializedHigherPriorityFactoryOverridesGenericBoundedFallback()
    {
        var specialized = new TrackingFactory(priority: 50);
        var catalog = new FileEditHistoryProviderCatalog(
            [new BoundedTextHistoryProviderFactory(), specialized]);

        await using var history = await catalog.CreateAsync(Profile(history: true), Request());

        Assert.NotNull(history);
        Assert.Equal(1, specialized.CreateCalls);
        Assert.Equal(-100, new BoundedTextHistoryProviderFactory().Priority);
    }

    [Fact]
    public async Task CreateAsync_EqualHighestPriorityFactoriesAreAmbiguousRegardlessOfLowerMatches()
    {
        var catalog = new FileEditHistoryProviderCatalog(
            [new TrackingFactory(10), new TrackingFactory(-20), new TrackingFactory(10)]);

        var exception = await Assert.ThrowsAsync<FileEditHistoryFactoryAmbiguityException>(
            () => catalog.CreateAsync(Profile(history: true), Request()).AsTask());

        Assert.Equal(2, exception.MatchingFactoryCount);
        Assert.Equal(10, exception.Priority);
    }

    private static FileInteractionProfileDescriptor Profile(bool history)
        => new(
            "text",
            FileInteractionCapabilities.View
                | FileInteractionCapabilities.Edit
                | FileInteractionCapabilities.Save
                | FileInteractionCapabilities.Undo
                | FileInteractionCapabilities.Redo,
            extensions: [".txt"],
            history: history ? new FileHistoryOptions(10, 100) : FileHistoryOptions.Disabled);

    private static FileInteractionRequest Request()
        => new(FileEditSessionTests.File(), "file.txt", FileInteractionMode.Edit, "text/plain");

    private sealed class AlwaysFactory : IFileEditHistoryProviderFactory
    {
        public bool CanCreate(FileInteractionProfileDescriptor profile, FileInteractionRequest request) => true;

        public ValueTask<IFileEditHistoryProvider?> CreateAsync(
            FileInteractionProfileDescriptor profile,
            FileInteractionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IFileEditHistoryProvider?>(
                new BoundedTextHistoryProvider(new FileHistoryOptions(2, 10)));
    }

    private sealed class TrackingFactory(int priority) : IFileEditHistoryProviderFactory
    {
        public int Priority => priority;

        public int CreateCalls { get; private set; }

        public bool CanCreate(FileInteractionProfileDescriptor profile, FileInteractionRequest request)
            => true;

        public ValueTask<IFileEditHistoryProvider?> CreateAsync(
            FileInteractionProfileDescriptor profile,
            FileInteractionRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return ValueTask.FromResult<IFileEditHistoryProvider?>(
                new BoundedTextHistoryProvider(new FileHistoryOptions(2, 100)));
        }
    }
}
