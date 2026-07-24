using System.Text;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.FileInteraction.Markdown.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using FileInteractionComponent = CanDoItAll.FileTools.FileInteraction.Components.FileInteraction;

namespace CanDoItAll.FileTools.FileInteraction.Markdown.Tests;

public sealed class MarkdownSecurityAndInteractionTests : BunitContext
{
    private const string ActiveElementSelector =
        "a, area, audio, base, embed, form, iframe, img, input, link, meta, object, script, source, style, svg, video";

    private const string ActiveAttributeSelector =
        "[action], [data], [formaction], [href], [onclick], [onerror], [onfocus], [onload], [onmouseover], "
        + "[poster], [src], [srcset], [style]";

    public static TheoryData<string> AdversarialMarkdown => new()
    {
        """
        <script src="https://attacker.test/script.js">danger()</script>
        <style>@import url(https://attacker.test/style.css);</style>
        <link rel="stylesheet" href="https://attacker.test/theme.css">
        <meta http-equiv="refresh" content="0;url=https://attacker.test/redirect">
        <iframe src="https://attacker.test/frame"></iframe>
        <object data="data:text/html,danger"></object>
        <svg><use href="https://attacker.test/icons.svg#danger"></use></svg>
        """,
        """
        [remote](https://attacker.test/page)
        [script](javascript:alert(1))
        [data](data:text/html,danger)
        [blob](blob:https://attacker.test/id)
        ![remote image](https://attacker.test/image.png)
        [![nested image](https://attacker.test/nested.png)](https://attacker.test/nested-link)
        <https://attacker.test/autolink>
        <user@attacker.test>
        https://attacker.test/bare
        """,
        """
        [mixed case](JaVaScRiPt:alert(1))
        [entity scheme](jav&#x61;script&#x3a;alert(1))
        [percent scheme](java%73cript%3Aalert(1))
        [tab scheme](java	script:alert(1))
        [newline scheme](java
        script:alert(1))
        [attribute-shaped label](\" onmouseover=\"danger())
        """,
        """
        [reference][remote]
        ![reference image][image]

        [remote]: https://attacker.test/reference "\" onmouseover=\"danger()"
        [image]: data:image/svg+xml,<svg/onload=danger()>
        """,
        """
        [broken](<https://attacker.test/no-close
        ![broken image](https://attacker.test/no-close
        &lt;img src=&quot;https://attacker.test/entity.png&quot; onerror=&quot;danger()&quot;&gt;
        <a href="java&#x73;cript:danger()">encoded raw link</a>
        """,
        """
        ```foo" onmouseover="danger()"><img src="https://attacker.test/fence.png">
        <script src="https://attacker.test/code.js"></script>
        ```
        """
    };

    [Theory]
    [MemberData(nameof(AdversarialMarkdown))]
    public void AdversarialContent_CannotCreateNavigationFetchOrExecutableDom(string markdown)
    {
        var cut = RenderMarkdown(markdown);

        Assert.Empty(cut.FindAll(ActiveElementSelector));
        Assert.Empty(cut.FindAll(ActiveAttributeSelector));
    }

    [Fact]
    public async Task EditorInput_UpdatesDebouncedMarkdownPreviewThroughRegisteredViewRenderer()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();
        var request = new FileInteractionRequest(
            new FileReference("test", "README.md"),
            "README.md",
            FileInteractionMode.Edit,
            "text/markdown");
        var cut = Render<FileInteractionComponent>(parameters => parameters
            .Add(component => component.Request, request)
            .Add(component => component.ContentSource, new StringContentSource("# Initial"))
            .Add(component => component.Composition, composition));

        Assert.Equal("Initial", cut.Find("[data-testid='interaction-preview'] h1").TextContent);

        await cut.Find("textarea").InputAsync("## Updated preview");

        cut.WaitForAssertion(
            () => Assert.Equal(
                "Updated preview",
                cut.Find("[data-testid='interaction-preview'] h2").TextContent),
            TimeSpan.FromSeconds(2));
        Assert.Empty(cut.FindAll("[data-testid='interaction-preview'] h1"));
    }

    [Fact]
    public void AdvancedMarkdownAndRegisteredFencedCode_RenderThroughMarkdigAndTypedComponent()
    {
        Services.AddSingleton<IMarkdownFencedCodeComponentRegistration>(new TestMermaidRegistration());
        const string markdown = """
            # Architecture

            | Layer | Owner |
            | --- | --- |
            | UI | Blazor |

            ~~Legacy renderer~~

            See [the architecture][architecture].

            ```mermaid
            flowchart LR
                UI --> Application
            ```

            [architecture]: https://example.test/architecture
            """;

        var cut = RenderMarkdown(markdown);

        Assert.Equal("Architecture", cut.Find("h1").TextContent);
        Assert.Equal("UI", cut.Find("table tbody tr td").TextContent);
        Assert.Equal("Legacy renderer", cut.Find("del").TextContent);
        Assert.Equal("the architecture", cut.Find(".cdi-ft-markdown__link-label").TextContent);
        Assert.Empty(cut.FindAll("a"));
        var mermaid = cut.FindComponent<TestMermaidComponent>();
        Assert.Equal("mermaid", mermaid.Instance.Context.Language);
        Assert.Equal(
            "flowchart LR\n    UI --> Application",
            mermaid.Instance.Context.Source.ReplaceLineEndings("\n"));
    }

    private IRenderedComponent<MarkdownFileView> RenderMarkdown(string markdown)
        => Render<MarkdownFileView>(parameters => parameters.Add(
            component => component.Context,
            Context(markdown)));

    private static FileInteractionRenderContext Context(string markdown)
        => new(
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

    private sealed class TestMermaidRegistration : IMarkdownFencedCodeComponentRegistration
    {
        public string Language => "mermaid";

        public Type ComponentType => typeof(TestMermaidComponent);
    }

    private sealed class TestMermaidComponent : ComponentBase, IMarkdownFencedCodeComponent
    {
        [Parameter, EditorRequired]
        public MarkdownFencedCodeRenderContext Context { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "pre");
            builder.AddAttribute(1, "data-testid", "test-mermaid-component");
            builder.AddContent(2, Context.Source);
            builder.CloseElement();
        }
    }
}
