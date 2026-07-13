using System.Reflection;
using Microsoft.AspNetCore.Components;
using FileBrowserComponent = CanDoItAll.FileTools.FileBrowser.Components.FileBrowser;

namespace CanDoItAll.FileTools.FileBrowser.Components.Tests;

public sealed class FileBrowserComponentContractTests
{
    [Fact]
    public void DisplayModes_AreExplicitAndStable()
        => Assert.Equal(
            [FileBrowserDisplayMode.Standard, FileBrowserDisplayMode.Compact, FileBrowserDisplayMode.Minimal],
            Enum.GetValues<FileBrowserDisplayMode>());

    [Fact]
    public void RecursiveBrowseLabel_IsGenericAndHostOverrideable()
    {
        var component = new FileBrowserComponent();
        PropertyInfo property = typeof(FileBrowserComponent).GetProperty(
            nameof(FileBrowserComponent.IncludeDescendantsLabel))!;

        Assert.Equal("Include descendants", component.IncludeDescendantsLabel);
        Assert.NotNull(property.GetCustomAttribute<ParameterAttribute>());
        Assert.True(property.CanWrite);
    }

    [Fact]
    public void ComponentEvents_UseEventCallbackOnly()
    {
        Type[] componentTypes = typeof(FileBrowserComponent).Assembly
            .GetTypes()
            .Where(type => typeof(ComponentBase).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();
        PropertyInfo[] parameters = componentTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null)
            .ToArray();
        PropertyInfo[] eventParameters = parameters
            .Where(property => property.Name.EndsWith("Changed", StringComparison.Ordinal)
                || property.Name.EndsWith("Invoked", StringComparison.Ordinal)
                || property.Name.EndsWith("Requested", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(eventParameters);
        Assert.All(eventParameters, property => Assert.True(
            property.PropertyType == typeof(EventCallback)
                || property.PropertyType.IsGenericType
                    && property.PropertyType.GetGenericTypeDefinition() == typeof(EventCallback<>),
            $"{property.DeclaringType?.Name}.{property.Name} must use EventCallback."));
        Assert.DoesNotContain(parameters, property => typeof(Delegate).IsAssignableFrom(property.PropertyType));
    }

    [Fact]
    public void PackageGraph_HasNoComponentsOrBaseLibDependency()
    {
        string project = File.ReadAllText(Path.Combine(
            TestPaths.ComponentsSource,
            "CanDoItAll.FileTools.FileBrowser.Components.csproj"));

        Assert.DoesNotContain("CanDoItAll.Components", project, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BaseLib", project, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CanDoItAll.FileTools.Abstractions", project, StringComparison.Ordinal);
        Assert.Contains("CanDoItAll.FileTools.FileBrowser.Core", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Sources_ContainNoBrowserOrProviderSideEffects()
    {
        string combined = string.Join(
            '\n',
            Directory.EnumerateFiles(TestPaths.ComponentsSource, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetExtension(path) is ".razor" or ".cs" or ".js")
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        string[] forbidden =
        [
            "href=", "download=", "navigator.clipboard", "window.open", "NavigationManager",
            "IJSRuntime", ".ExecuteActionAsync(", ".OpenUri", ".DownloadUri"
        ];
        Assert.All(forbidden, value => Assert.DoesNotContain(value, combined, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("await hostCallback.InvokeAsync", combined, StringComparison.Ordinal);
        Assert.Contains("await ActionRequested.InvokeAsync", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void Assets_AreScopedAndDoNotInstallGlobals()
    {
        Assert.True(File.Exists(Path.Combine(TestPaths.ComponentsSource, "Components", "FileBrowser.razor.css")));
        Assert.False(Directory.Exists(Path.Combine(TestPaths.ComponentsSource, "wwwroot")));
        Assert.Empty(Directory.EnumerateFiles(TestPaths.ComponentsSource, "*.js", SearchOption.AllDirectories));
        Assert.DoesNotContain(
            "style=",
            string.Join('\n', Directory.EnumerateFiles(TestPaths.ComponentsSource, "*.razor", SearchOption.AllDirectories)
                .Select(File.ReadAllText)),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionMenu_UsesTopLayerPopoverInsteadOfClippableOverlay()
    {
        string markup = File.ReadAllText(Path.Combine(
            TestPaths.ComponentsSource,
            "Components",
            "FileBrowserItemActions.razor"));
        string styles = File.ReadAllText(Path.Combine(
            TestPaths.ComponentsSource,
            "Components",
            "FileBrowser.razor.css"));

        Assert.Contains("popover=\"auto\"", markup, StringComparison.Ordinal);
        Assert.Contains("popovertarget=", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"group\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"menu\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"menuitem\"", markup, StringComparison.Ordinal);
        Assert.Contains(":popover-open", styles, StringComparison.Ordinal);
        Assert.Contains("position-area", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Styles_RespondToHostContainerAndRespectReducedMotion()
    {
        string styles = File.ReadAllText(Path.Combine(
            TestPaths.ComponentsSource,
            "Components",
            "FileBrowser.razor.css"));

        Assert.Contains("container-name: ft-file-browser", styles, StringComparison.Ordinal);
        Assert.Contains("container-type: inline-size", styles, StringComparison.Ordinal);
        Assert.Contains("@container ft-file-browser", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", styles, StringComparison.Ordinal);
        Assert.Contains("animation: none", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void MinimalStyles_PreserveResultSpaceInShortFloatingWindows()
    {
        string styles = File.ReadAllText(Path.Combine(
            TestPaths.ComponentsSource,
            "Components",
            "FileBrowser.razor.css"));

        Assert.Contains(
            ".ft-file-browser[data-display-mode=\"minimal\"] ::deep .ft-file-browser__source-select-label > span",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".ft-file-browser[data-display-mode=\"minimal\"] .ft-file-browser__paging",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("min-height: 5.4rem", styles, StringComparison.Ordinal);
        Assert.Contains(".ft-file-browser__results-region", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void CompactStyles_BoundSourceNavigationToItsContentHeight()
    {
        string markup = File.ReadAllText(Path.Combine(
            TestPaths.ComponentsSource,
            "Components",
            "FileBrowser.razor"));
        string styles = File.ReadAllText(Path.Combine(
            TestPaths.ComponentsSource,
            "Components",
            "FileBrowser.razor.css"));

        Assert.Contains("has-source-navigation", markup, StringComparison.Ordinal);
        Assert.Contains(
            ".ft-file-browser[data-display-mode=\"compact\"] .ft-file-browser__layout.has-source-navigation",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("grid-template-rows: auto minmax(0, 1fr);", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Markup_CoversOperationalStatesAndProviderNeutralRecursion()
    {
        string rootMarkup = File.ReadAllText(Path.Combine(TestPaths.ComponentsSource, "Components", "FileBrowser.razor"));
        string toolbarMarkup = File.ReadAllText(Path.Combine(TestPaths.ComponentsSource, "Components", "FileBrowserToolbar.razor"));
        string styles = File.ReadAllText(Path.Combine(TestPaths.ComponentsSource, "Components", "FileBrowser.razor.css"));

        Assert.Contains("No sources configured", rootMarkup, StringComparison.Ordinal);
        Assert.Contains("has-no-sources", rootMarkup, StringComparison.Ordinal);
        Assert.Contains(".ft-file-browser__layout.has-no-sources", styles, StringComparison.Ordinal);
        Assert.Contains("Connecting to source", rootMarkup, StringComparison.Ordinal);
        Assert.Contains("This folder is empty", rootMarkup, StringComparison.Ordinal);
        Assert.Contains("No matching items", rootMarkup, StringComparison.Ordinal);
        Assert.Contains("Load more", rootMarkup, StringComparison.Ordinal);
        Assert.Contains("IncludeDescendantsLabel", toolbarMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("subproject", toolbarMarkup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainCodeBehind_IsMateriallySmallerThanLegacyComponent()
    {
        string path = Path.Combine(TestPaths.ComponentsSource, "Components", "FileBrowser.razor.cs");
        int lineCount = File.ReadLines(path).Count();

        Assert.True(lineCount < 400, $"Expected fewer than 400 lines but found {lineCount}.");
    }
}
