using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.FileInteraction.Markdown.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.FileTools.FileInteraction.Markdown.Tests;

public sealed class MarkdownCompositionTests
{
    [Theory]
    [InlineData("README.md", FileInteractionBuiltInProfileIds.Text)]
    [InlineData("guide.markdown", FileInteractionBuiltInProfileIds.Object)]
    public void AddMarkdown_ProfileWinsOverBaseOnlyWhenExplicitlyRegistered(
        string fileName,
        string expectedBaseProfile)
    {
        var request = Request(fileName);
        var baseComposition = new FileInteractionComponentBuilder().AddBuiltIns().Build();
        var markdownComposition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();

        Assert.Equal(
            expectedBaseProfile,
            baseComposition.Core.Profiles.Resolve(request).Profile!.Id);
        Assert.Equal(
            FileInteractionMarkdownProfileIds.Markdown,
            markdownComposition.Core.Profiles.Resolve(request).Profile!.Id);
    }

    [Fact]
    public void AddMarkdown_ExactMediaTypeWinsOverBaseTextWildcard()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();
        var request = new FileInteractionRequest(
            new FileReference("test", "README"),
            "README",
            mediaType: "text/markdown; charset=utf-8");

        var resolution = composition.Core.Profiles.Resolve(request);

        Assert.Equal(FileInteractionMarkdownProfileIds.Markdown, resolution.Profile!.Id);
        Assert.Equal(FileInteractionMatchKind.MediaTypeExact, resolution.Candidates.Single().MatchKind);
    }

    [Fact]
    public void AddMarkdown_ViewAndEditRenderersResolveAndEditReusesBaseTextEditor()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();

        var view = composition.Renderers.Resolve(
            FileInteractionMarkdownProfileIds.Markdown,
            FileInteractionMode.View);
        var edit = composition.Renderers.Resolve(
            FileInteractionMarkdownProfileIds.Markdown,
            FileInteractionMode.Edit);

        Assert.Equal(typeof(MarkdownFileView), view.Renderer!.ComponentType);
        Assert.Equal(typeof(TextFileEditor), edit.Renderer!.ComponentType);
        Assert.Equal(FileInteractionContentKind.Text, view.Renderer.ContentKind);
        Assert.Equal(FileInteractionContentKind.Text, edit.Renderer.ContentKind);
    }

    [Fact]
    public void AddMarkdown_AloneRegistersOnlyTheOptionalProfileAndItsTwoSurfaces()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddMarkdown()
            .Build();

        var profile = Assert.Single(composition.Core.Profiles.Profiles);

        Assert.Equal(FileInteractionMarkdownProfileIds.Markdown, profile.Id);
        Assert.Equal(2, composition.Renderers.Renderers.Count);
        Assert.All(
            composition.Renderers.Renderers,
            renderer => Assert.Equal(FileInteractionMarkdownProfileIds.Markdown, renderer.ProfileId));
    }

    [Fact]
    public void AddMarkdown_ProfilePrecedenceIsIndependentOfContributionOrder()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddMarkdown()
            .AddBuiltIns()
            .Build();

        var resolution = composition.Core.Profiles.Resolve(Request("README.md"));

        Assert.Equal(FileInteractionMarkdownProfileIds.Markdown, resolution.Profile!.Id);
    }

    [Fact]
    public void AddMarkdown_ProfileEnablesEditingPreviewPersistenceAndHistory()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();
        var profile = composition.Core.Profiles.Profiles.Single(
            candidate => candidate.Id == FileInteractionMarkdownProfileIds.Markdown);

        Assert.Equal(
            FileInteractionCapabilities.View
                | FileInteractionCapabilities.Edit
                | FileInteractionCapabilities.Preview
                | FileInteractionCapabilities.Save
                | FileInteractionCapabilities.Undo
                | FileInteractionCapabilities.Redo,
            profile.Capabilities);
        Assert.True(profile.Preview.Enabled);
        Assert.True(profile.Preview.SplitByDefault);
        Assert.Equal(TimeSpan.FromMilliseconds(300), profile.Preview.Debounce);
        Assert.True(profile.History.Enabled);
    }

    [Fact]
    public void AddFileInteractionComponents_CanExplicitlyComposeBuiltInsAndMarkdown()
    {
        var services = new ServiceCollection();
        services.AddFileInteractionComponents(builder => builder
            .AddBuiltIns()
            .AddMarkdown());
        using var provider = services.BuildServiceProvider();

        var composition = provider.GetRequiredService<FileInteractionComponentComposition>();
        var resolution = composition.Core.Profiles.Resolve(Request("README.md"));

        Assert.Equal(FileInteractionMarkdownProfileIds.Markdown, resolution.Profile!.Id);
        Assert.Equal(
            typeof(MarkdownFileView),
            composition.Renderers.Resolve(
                resolution.Profile.Id,
                FileInteractionMode.View).Renderer!.ComponentType);
    }

    [Fact]
    public async Task AddMarkdown_WithBuiltInsCreatesBoundedUndoRedoHistory()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .Build();
        var profile = composition.Core.Profiles.Profiles.Single(
            candidate => candidate.Id == FileInteractionMarkdownProfileIds.Markdown);
        var request = new FileInteractionRequest(
            new FileReference("test", "README.md"),
            "README.md",
            FileInteractionMode.Edit,
            "text/markdown");

        await using var history = await composition.Core.HistoryProviders.CreateAsync(profile, request);

        Assert.IsType<BoundedTextHistoryProvider>(history);
    }

    [Fact]
    public void PackageGraph_OnlyOptionalAdapterReferencesMarkdig()
    {
        var adapterReferences = typeof(MarkdownFileView).Assembly.GetReferencedAssemblies();
        var baseReferences = new[]
        {
            typeof(FileInteractionComponentComposition).Assembly,
            typeof(FileInteractionProfileCatalog).Assembly,
            typeof(FileInteractionRequest).Assembly
        };

        Assert.Contains(adapterReferences, reference => reference.Name == "Markdig");
        foreach (var assembly in baseReferences.Distinct())
        {
            Assert.DoesNotContain(
                assembly.GetReferencedAssemblies(),
                reference => reference.Name?.Contains("Markdig", StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    private static FileInteractionRequest Request(string fileName)
        => new(new FileReference("test", fileName), fileName);
}
