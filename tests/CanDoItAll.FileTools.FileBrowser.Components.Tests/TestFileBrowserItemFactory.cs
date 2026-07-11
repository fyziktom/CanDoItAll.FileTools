namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

internal static class TestFileBrowserItemFactory
{
    public static FileBrowserItem Create(
        string name = "notes.md",
        FileBrowserItemKind kind = FileBrowserItemKind.File,
        FileBrowserItemCapabilities capabilities = FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Open,
        FileBrowserItemCategory? category = null)
    {
        var sourceId = new FileBrowserSourceId("test");
        return new FileBrowserItem(
            new FileBrowserItemKey(sourceId, $"/{name}"),
            new FileBrowserItemKey(sourceId, "/"),
            name,
            kind,
            category ?? (kind == FileBrowserItemKind.Container
                ? FileBrowserItemCategory.Folder
                : FileBrowserItemCategory.Document),
            displayPath: name,
            size: kind == FileBrowserItemKind.File ? 1536 : null,
            mediaType: kind == FileBrowserItemKind.File ? "text/markdown" : null,
            modifiedAt: new DateTimeOffset(2026, 7, 11, 12, 30, 0, TimeSpan.Zero),
            capabilities: capabilities);
    }
}
