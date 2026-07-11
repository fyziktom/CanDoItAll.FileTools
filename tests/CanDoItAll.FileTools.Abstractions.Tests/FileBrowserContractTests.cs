namespace CanDoItAll.FileTools.Abstractions.Tests;

public sealed class FileBrowserContractTests
{
    [Fact]
    public void Identity_NormalizesOpaqueValues_WithoutCollapsingOccurrences()
    {
        var source = new FileBrowserSourceId(" project-files ");
        var first = new FileBrowserItemKey(source, " projects/one/readme ", " r2 ");
        var second = new FileBrowserItemKey(source, "projects/two/readme", "r2");
        var content = new FileBrowserContentIdentity(" CID ", "bafy-shared");

        Assert.Equal("project-files", source.Value);
        Assert.Equal("projects/one/readme", first.Value);
        Assert.Equal("r2", first.Revision);
        Assert.NotEqual(first, second);
        Assert.Equal("cid:bafy-shared", content.ToString());
    }

    [Fact]
    public void Identity_RejectsMissingSourceOccurrenceAndContentValues()
    {
        Assert.Throws<ArgumentException>(() => new FileBrowserSourceId(" "));
        Assert.Throws<ArgumentException>(() => new FileBrowserItemKey(default, "item"));
        Assert.Throws<ArgumentException>(() => new FileBrowserItemKey(Source(), ""));
        Assert.Throws<ArgumentException>(() => new FileBrowserContentIdentity("cid", " "));
    }

    [Fact]
    public void Item_DefensivelyCopiesMetadata_AndRejectsUnsafeUri()
    {
        var metadata = new Dictionary<string, string> { ["provider"] = "project" };
        var item = File("spec", "Specification.md", metadata: metadata, openUri: "/viewer/spec");
        metadata["provider"] = "mutated";

        Assert.Equal("project", item.Metadata["provider"]);
        Assert.Equal("/viewer/spec", item.OpenUri);
        Assert.Throws<ArgumentException>(() => File("unsafe", "unsafe.txt", openUri: "javascript:alert(1)"));
    }

    [Fact]
    public void SourceDescriptor_NativeSearchRequiresProviderScope()
    {
        Assert.Throws<ArgumentException>(() => new FileBrowserSourceDescriptor(
            Source(),
            "Source",
            capabilities: FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
            supportedSearchScopes: [FileBrowserSearchScope.LoadedFolder]));

        var descriptor = new FileBrowserSourceDescriptor(
            Source(),
            " Project files ",
            capabilities: FileBrowserSourceCapabilities.PagedBrowse | FileBrowserSourceCapabilities.NativeSearch,
            supportedSearchScopes: [FileBrowserSearchScope.Provider]);

        Assert.Equal("Project files", descriptor.DisplayName);
        Assert.True(descriptor.Supports(FileBrowserSourceCapabilities.NativeSearch));
    }

    [Fact]
    public void Filter_NormalizesExtensions_AndKeepsContainersNavigable()
    {
        var filter = new FileBrowserFilter(extensions: [" CS ", ".cs"]);
        var parent = Key("root");
        var folder = new FileBrowserItem(
            Key("src"), parent, "src", FileBrowserItemKind.Container, FileBrowserItemCategory.Folder,
            childState: FileBrowserChildState.HasChildren);

        Assert.Equal([".cs"], filter.Extensions);
        Assert.True(filter.Matches(folder));
        Assert.True(filter.Matches(File("code", "Program.CS", parent)));
        Assert.False(filter.Matches(File("doc", "README.md", parent)));
    }

    [Fact]
    public void QueryFingerprint_IgnoresCursor_ButIncludesSemanticDimensions()
    {
        var parent = Key("root", revision: "r7");
        var first = new FileBrowserBrowseRequest(
            parent,
            50,
            continuationToken: "page-1",
            filter: new FileBrowserFilter(extensions: [".json", "cs"]),
            consistencyToken: "token-a");
        var continued = new FileBrowserBrowseRequest(
            parent,
            50,
            continuationToken: "page-2",
            filter: new FileBrowserFilter(extensions: ["CS", ".JSON"]),
            consistencyToken: "token-b");
        var descendants = new FileBrowserBrowseRequest(parent, 50, includeDescendants: true);

        Assert.Equal(FileBrowserQueryFingerprint.From(first), FileBrowserQueryFingerprint.From(continued));
        Assert.NotEqual(FileBrowserQueryFingerprint.From(first), FileBrowserQueryFingerprint.From(descendants));
        Assert.Equal(64, FileBrowserQueryFingerprint.From(first).Value.Length);
    }

    [Fact]
    public void Requests_RejectInvalidBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserBrowseRequest(Key("root"), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserReadRequest(Key("file"), Offset: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserReadRequest(Key("file"), Length: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileBrowserPage([], totalCount: -1));
    }

    [Fact]
    public async Task ContentLease_HonorsOwnership()
    {
        var borrowedStream = new MemoryStream([1, 2, 3]);
        await new FileBrowserContentLease(borrowedStream, ownsStream: false).DisposeAsync();
        Assert.True(borrowedStream.CanRead);

        var ownedStream = new MemoryStream([4, 5]);
        var ownedLease = new FileBrowserContentLease(ownedStream);
        await ownedLease.DisposeAsync();
        await ownedLease.DisposeAsync();
        Assert.False(ownedStream.CanRead);
    }

    [Fact]
    public void VersionStamp_IsDescriptive_NotACachePolicy()
    {
        var stamp = new FileBrowserVersionStamp(" cid:bafy ", isImmutable: true);

        Assert.Equal("cid:bafy", stamp.Value);
        Assert.True(stamp.IsImmutable);
    }

    private static FileBrowserSourceId Source(string value = "source") => new(value);

    private static FileBrowserItemKey Key(string value, string? revision = null)
        => new(Source(), value, revision);

    private static FileBrowserItem File(
        string key,
        string name,
        FileBrowserItemKey? parent = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? openUri = null)
        => new(
            Key(key),
            parent,
            name,
            FileBrowserItemKind.File,
            FileBrowserItemCategory.Document,
            childState: FileBrowserChildState.Empty,
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Open,
            metadata: metadata,
            openUri: openUri);
}
