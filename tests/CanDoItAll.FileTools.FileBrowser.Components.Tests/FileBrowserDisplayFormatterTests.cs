using System.Globalization;

namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserDisplayFormatterTests
{
    [Theory]
    [InlineData(null, "\u2014")]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1,023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    public void FormatSize_UsesCompactBinaryUnits(long? bytes, string expected)
        => Assert.Equal(expected, FileBrowserDisplayFormatter.FormatSize(bytes));

    [Fact]
    public void FormatSize_UsesInvariantOutputWhenCurrentCultureUsesDifferentSeparators()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");

            Assert.Equal("1,023 B", FileBrowserDisplayFormatter.FormatSize(1023));
            Assert.Equal("1.5 KB", FileBrowserDisplayFormatter.FormatSize(1536));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void FormatType_PrefersFolderThenMediaType()
    {
        FileBrowserItem folder = TestFileBrowserItemFactory.Create(
            "docs",
            FileBrowserItemKind.Container,
            FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
        FileBrowserItem file = TestFileBrowserItemFactory.Create();

        Assert.Equal("Folder", FileBrowserDisplayFormatter.FormatType(folder));
        Assert.Equal("text/markdown", FileBrowserDisplayFormatter.FormatType(file));
    }

    [Theory]
    [InlineData(FileBrowserSearchScope.LoadedFolder, "This loaded folder")]
    [InlineData(FileBrowserSearchScope.LoadedDescendants, "Loaded descendants")]
    [InlineData(FileBrowserSearchScope.Provider, "All source items")]
    [InlineData(FileBrowserSearchScope.Progressive, "Progressive deep search")]
    public void FormatScope_IsProviderNeutral(FileBrowserSearchScope scope, string expected)
        => Assert.Equal(expected, FileBrowserDisplayFormatter.FormatScope(scope));
}
