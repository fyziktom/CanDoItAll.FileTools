using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.FileInteraction.Markdown.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FileInteractionComponent = CanDoItAll.FileTools.FileInteraction.Components.FileInteraction;

namespace CanDoItAll.FileTools.FileInteraction.Markdown.Tests;

public sealed class MarkdownRenderingTests
{
    [Fact]
    public async Task Render_HeadingsEmphasisAndCodeUseTheMarkdownSurface()
    {
        const string markdown = "# Heading\n\nThis is **strong** and `code`.\n\n```csharp\nvar value = 1;\n```";

        var html = await RenderAsync(markdown);

        Assert.Contains("data-testid=\"interaction-markdown-view\"", html, StringComparison.Ordinal);
        Assert.Contains(">Heading</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<strong>strong</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<code>code</code>", html, StringComparison.Ordinal);
        Assert.Contains("<pre><code", html, StringComparison.Ordinal);
        Assert.Contains("var value = 1;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_AdvancedTablesAndStrikethroughUseMarkdigExtensions()
    {
        const string markdown = """
            | Layer | Owner |
            | --- | --- |
            | UI | Blazor |

            ~~Legacy renderer~~
            """;

        var html = await RenderAsync(markdown);

        Assert.Contains("<table>", html, StringComparison.Ordinal);
        Assert.Contains("<td>UI</td>", html, StringComparison.Ordinal);
        Assert.Contains("<del>Legacy renderer</del>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_RawHtmlCannotCreateActiveElementsOrEventHandlers()
    {
        const string markdown = """
            <script>alert('script')</script>
            <iframe src="https://example.test/frame"></iframe>
            <object data="data:text/html,danger"></object>
            <img src="https://example.test/pixel" onerror="danger()">
            <a href="javascript:danger()" onclick="danger()">raw link</a>
            """;

        var html = await RenderAsync(markdown);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Render_AllMarkdownLinksAndImagesAreInertButKeepLabels()
    {
        const string markdown = """
            [mixed case](JaVaScRiPt:alert(1))
            [leading whitespace](  javascript:alert(2) )
            [data link](DaTa:text/html,danger)
            [blob link](bLoB:https://example.test/id)
            [ordinary link](https://example.test/page)
            ![remote image](https://example.test/image.png)
            ![data image](dAtA:image/svg+xml,danger)
            """;

        var html = await RenderAsync(markdown);

        Assert.Contains("mixed case", html, StringComparison.Ordinal);
        Assert.Contains("leading whitespace", html, StringComparison.Ordinal);
        Assert.Contains("data link", html, StringComparison.Ordinal);
        Assert.Contains("blob link", html, StringComparison.Ordinal);
        Assert.Contains("ordinary link", html, StringComparison.Ordinal);
        Assert.Contains("Image: remote image", html, StringComparison.Ordinal);
        Assert.Contains("Image: data image", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("blob:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://example.test", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Render_AutolinkIsAlsoInert()
    {
        var html = await RenderAsync("Visit <https://example.test/path>.");

        Assert.Contains("https://example.test/path", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Render_EditSurfaceUsesMarkdownViewAsDefaultSplitPreview()
    {
        const string markdown = "# Live preview";
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();
        var request = new FileInteractionRequest(
            new FileReference("test", "README.md"),
            "README.md",
            FileInteractionMode.Edit,
            "text/markdown");
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(FileInteractionComponent.Request)] = request,
            [nameof(FileInteractionComponent.ContentSource)] = new StringContentSource(markdown),
            [nameof(FileInteractionComponent.Composition)] = composition
        });

        var html = await RenderComponentAsync<FileInteractionComponent>(parameters);

        Assert.Contains("data-mode=\"edit\"", html, StringComparison.Ordinal);
        Assert.Contains("data-preview=\"shown\"", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"interaction-text-editor\"", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"interaction-preview\"", html, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"interaction-markdown-view\"", html, StringComparison.Ordinal);
        Assert.Contains(">Live preview</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_FullViewShellWithMarkdownMediaTypeSelectsMarkdownRenderer()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();
        var request = new FileInteractionRequest(
            new FileReference("test", "README.md"),
            "README.md",
            FileInteractionMode.View,
            "text/markdown");
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(FileInteractionComponent.Request)] = request,
            [nameof(FileInteractionComponent.ContentSource)] = new StringContentSource("# Rendered heading"),
            [nameof(FileInteractionComponent.Composition)] = composition
        });

        var html = await RenderComponentAsync<FileInteractionComponent>(parameters);

        Assert.Contains("data-testid=\"interaction-markdown-view\"", html, StringComparison.Ordinal);
        Assert.Contains(">Rendered heading</h1>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-testid=\"interaction-text-view\"", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderAsync(string markdown)
    {
        var context = new FileInteractionRenderContext(
            new FileInteractionRequest(
                new FileReference("test", "README.md"),
                "README.md",
                mediaType: "text/markdown"),
            FileInteractionMode.View,
            Encoding.UTF8.GetBytes(markdown),
            editRevision: 0,
            mediaType: "text/markdown",
            text: markdown,
            encodingName: "utf-8");
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(MarkdownFileView.Context)] = context
        });
        return await RenderComponentAsync<MarkdownFileView>(parameters);
    }

    private static async Task<string> RenderComponentAsync<TComponent>(ParameterView parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        try
        {
            return await renderer.Dispatcher.InvokeAsync(async () =>
            {
                var root = await renderer.RenderComponentAsync<TComponent>(parameters);
                return root.ToHtmlString();
            });
        }
        finally
        {
            await services.DisposeAsync();
        }
    }

    private sealed class StringContentSource(string value) : IFileContentSource
    {
        public ValueTask<FileContentLease> OpenReadAsync(
            FileContentReadRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = Encoding.UTF8.GetBytes(value);
            return ValueTask.FromResult(new FileContentLease(
                new MemoryStream(content, writable: false),
                "text/markdown",
                content.Length));
        }
    }
}
