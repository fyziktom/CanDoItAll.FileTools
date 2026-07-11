namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserInteractionPolicyTests
{
    [Fact]
    public void NavigableContainer_IsActivatedInsideBrowser()
    {
        FileBrowserItem folder = TestFileBrowserItemFactory.Create(
            "src",
            FileBrowserItemKind.Container,
            FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);

        Assert.True(FileBrowserInteractionPolicy.CanActivate(folder));
        Assert.True(FileBrowserInteractionPolicy.NavigatesInternally(folder));
    }

    [Fact]
    public void OpenableFile_IsHostInvocationNotNavigation()
    {
        FileBrowserItem file = TestFileBrowserItemFactory.Create();

        Assert.True(FileBrowserInteractionPolicy.CanActivate(file));
        Assert.False(FileBrowserInteractionPolicy.NavigatesInternally(file));
        Assert.Contains("double-click to open", FileBrowserInteractionPolicy.BuildMainLabel(file));
    }

    [Fact]
    public void SelectOnlyLink_IsNotActivatable()
    {
        FileBrowserItem link = TestFileBrowserItemFactory.Create(
            "linked-folder",
            FileBrowserItemKind.Link,
            FileBrowserItemCapabilities.Select,
            FileBrowserItemCategory.Link);

        Assert.True(FileBrowserInteractionPolicy.CanSelect(link));
        Assert.False(FileBrowserInteractionPolicy.CanActivate(link));
    }

    [Fact]
    public void OpenOnlyItem_UsesStandardPrimaryButtonLabel()
    {
        FileBrowserItem item = TestFileBrowserItemFactory.Create(
            "open-only.md",
            capabilities: FileBrowserItemCapabilities.Open);

        Assert.Equal("Open open-only.md", FileBrowserInteractionPolicy.BuildMainLabel(item));
    }
}
