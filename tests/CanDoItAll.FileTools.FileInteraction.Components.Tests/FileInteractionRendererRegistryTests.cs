using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileInteractionRendererRegistryTests
{
    [Fact]
    public void Resolve_HigherPriorityRendererWinsDeterministically()
    {
        var registry = new FileInteractionRendererRegistry(
        [
            Descriptor("low", priority: 1),
            Descriptor("high", priority: 20)
        ]);

        var result = registry.Resolve("profile", FileInteractionMode.View);

        Assert.Equal(FileInteractionRendererResolutionStatus.Resolved, result.Status);
        Assert.Equal("high", result.Renderer!.Id);
    }

    [Fact]
    public void Resolve_EqualPriorityRenderersReportsAmbiguity()
    {
        var registry = new FileInteractionRendererRegistry(
        [
            Descriptor("alpha"),
            Descriptor("beta")
        ]);

        var result = registry.Resolve("profile", FileInteractionMode.View);

        Assert.Equal(FileInteractionRendererResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.Renderer);
        Assert.Equal(["alpha", "beta"], result.Candidates.Select(candidate => candidate.Id));
    }

    [Fact]
    public void Descriptor_ComponentWithoutRendererContractIsRejected()
    {
        var exception = Assert.Throws<ArgumentException>(() => new FileInteractionRendererDescriptor(
            "invalid",
            "profile",
            FileInteractionMode.View,
            typeof(ComponentBase),
            FileInteractionContentKind.Text));

        Assert.Contains(nameof(IFileInteractionRendererComponent), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Descriptor_ContextWithoutParameterAttributeIsRejectedBeforeDynamicRendering()
    {
        var exception = Assert.Throws<ArgumentException>(() => new FileInteractionRendererDescriptor(
            "invalid-parameter",
            "profile",
            FileInteractionMode.View,
            typeof(RendererWithoutContextParameter),
            FileInteractionContentKind.Text));

        Assert.Contains("Blazor parameter", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Descriptor_MetadataOnlyEditorIsRejectedButFullContentBinaryEditorIsSupported()
    {
        Assert.Throws<ArgumentException>(() => new FileInteractionRendererDescriptor(
            "metadata-editor",
            "profile",
            FileInteractionMode.Edit,
            typeof(TestFileInteractionRenderer),
            FileInteractionContentKind.Binary,
            contentRequirement: FileInteractionContentRequirement.MetadataOnly));

        var descriptor = new FileInteractionRendererDescriptor(
            "binary-editor",
            "profile",
            FileInteractionMode.Edit,
            typeof(TestFileInteractionRenderer),
            FileInteractionContentKind.Binary);

        Assert.Equal(FileInteractionContentKind.Binary, descriptor.ContentKind);
        Assert.Equal(FileInteractionContentRequirement.FullContent, descriptor.ContentRequirement);
    }

    [Fact]
    public void ContentChange_DefensivelyCopiesRendererOwnedBytesAndNormalizesOptionalMetadata()
    {
        byte[] bytes = [1, 2, 3];
        var change = new FileInteractionContentChange(bytes, " application/x-edit ", " binary-v1 ");

        bytes[0] = 9;

        Assert.Equal((byte)1, change.Content.Span[0]);
        Assert.Equal("application/x-edit", change.MediaType);
        Assert.Equal("binary-v1", change.EncodingName);
    }

    [Fact]
    public void Build_RendererForUnknownProfileFailsBeforeRuntime()
    {
        var builder = new FileInteractionComponentBuilder()
            .AddRenderer(Descriptor("orphan"));

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Contains("unknown profile", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuiltInComposition_HasNoOptionalMarkdownAssemblyReference()
    {
        _ = FileInteractionComponentComposition.BuiltIn;

        var references = typeof(FileInteractionComponentComposition).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain(references, name =>
            name?.Contains("Markdig", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void AddFileInteractionComponents_RegistersExplicitImmutableComposition()
    {
        var services = new ServiceCollection();
        services.AddFileInteractionComponents(builder => builder.AddBuiltIns());
        using var provider = services.BuildServiceProvider();

        var composition = provider.GetRequiredService<FileInteractionComponentComposition>();

        Assert.Contains(
            composition.Core.Profiles.Profiles,
            profile => profile.Id == FileInteractionBuiltInProfileIds.Text);
        Assert.Contains(
            composition.Renderers.Renderers,
            renderer => renderer.Id == "base-text-edit");
    }

    private static FileInteractionRendererDescriptor Descriptor(string id, int priority = 0)
        => new(
            id,
            "profile",
            FileInteractionMode.View,
            typeof(TestFileInteractionRenderer),
            FileInteractionContentKind.Text,
            priority);
}

public sealed class RendererWithoutContextParameter : ComponentBase, IFileInteractionRendererComponent
{
    public FileInteractionRenderContext Context { get; set; } = default!;
}

public sealed class TestFileInteractionRenderer : ComponentBase, IFileInteractionRendererComponent
{
    [Parameter]
    public FileInteractionRenderContext Context { get; set; } = default!;

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "data-testid", "custom-renderer");
        builder.AddContent(2, $"custom:{Context.Text}");
        builder.CloseElement();
    }
}
