using System.Text;
using CanDoItAll.FileTools.FileInteraction.Components;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileObjectViewZoomPanTests : FileToolsBunitContext
{
    [Fact]
    public async Task ImageView_WrapsTheImageInTheBuiltInZoomPanFrame()
    {
        SetupModules();
        var cut = Render<ImageFileView>(parameters => parameters
            .Add(component => component.Context, CreateContext("photo.png", "image/png", [1, 2, 3])));

        var surface = cut.Find("[data-testid='interaction-image-view']");
        Assert.Contains("cdi-ft-object-surface--zoom-pan", surface.ClassName);
        var frame = cut.FindComponent<FileZoomPanFrame>();
        Assert.False(frame.Instance.SuppressContentInteraction);
        Assert.Equal("Zoom and pan photo.png", frame.Instance.AriaLabel);
        var image = cut.Find("[data-testid='interaction-zoom-pan-content'] > img");
        Assert.Equal("false", image.GetAttribute("draggable"));
        Assert.False(cut.Find("[data-testid='interaction-zoom-pan-content']").HasAttribute("inert"));
        Assert.NotNull(cut.Find("[role='toolbar']"));

        await image.TriggerEventAsync("onload", EventArgs.Empty);

        Assert.False(surface.HasAttribute("hidden"));
    }

    [Fact]
    public void SvgView_UsesAnInertSandboxedFrameAndDeclaresTheIntrinsicSize()
    {
        SetupModules();
        var svg = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"900\" height=\"1600\"><rect width=\"900\" height=\"1600\"/></svg>");
        var cut = Render<SvgFileView>(parameters => parameters
            .Add(component => component.Context, CreateContext("poster.svg", "image/svg+xml", svg)));

        var frame = cut.FindComponent<FileZoomPanFrame>();
        Assert.True(frame.Instance.SuppressContentInteraction);
        var content = cut.Find("[data-testid='interaction-zoom-pan-content']");
        Assert.True(content.HasAttribute("inert"));
        var iframe = cut.Find("[data-testid='interaction-zoom-pan-content'] > iframe");
        Assert.Equal(string.Empty, iframe.GetAttribute("sandbox"));
        Assert.Equal("no-referrer", iframe.GetAttribute("referrerpolicy"));
        Assert.Equal("900", iframe.GetAttribute("data-cdi-ft-content-width"));
        Assert.Equal("1600", iframe.GetAttribute("data-cdi-ft-content-height"));
        cut.WaitForAssertion(() =>
            Assert.False(cut.Find("[data-testid='interaction-browser-view']").HasAttribute("hidden")));
    }

    [Fact]
    public void SvgView_WithoutADeclaredSizeOmitsTheIntrinsicAttributes()
    {
        SetupModules();
        var cut = Render<SvgFileView>(parameters => parameters
            .Add(component => component.Context, CreateContext(
                "fluid.svg",
                "image/svg+xml",
                Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100%\"></svg>"))));

        var iframe = cut.Find("iframe");
        Assert.False(iframe.HasAttribute("data-cdi-ft-content-width"));
        Assert.False(iframe.HasAttribute("data-cdi-ft-content-height"));
    }

    [Fact]
    public void ContentReplacement_RefreshesTheDeclaredSizeAndResetKey()
    {
        SetupModules();
        var first = CreateContext(
            "poster.svg",
            "image/svg+xml",
            Encoding.UTF8.GetBytes("<svg width=\"100\" height=\"50\"/>"));
        var second = CreateContext(
            "poster.svg",
            "image/svg+xml",
            Encoding.UTF8.GetBytes("<svg width=\"300\" height=\"75\"/>"),
            editRevision: 1);
        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, first)
            .Add(component => component.Kind, FileObjectViewKind.Browser)
            .Add(component => component.ZoomPan, FileObjectViewZoomPanOptions.Default));
        object? initialResetKey = cut.FindComponent<FileZoomPanFrame>().Instance.ResetKey;

        cut.Render(parameters => parameters
            .Add(component => component.Context, second)
            .Add(component => component.Kind, FileObjectViewKind.Browser)
            .Add(component => component.ZoomPan, FileObjectViewZoomPanOptions.Default));

        Assert.Equal("300", cut.Find("iframe").GetAttribute("data-cdi-ft-content-width"));
        Assert.Equal("75", cut.Find("iframe").GetAttribute("data-cdi-ft-content-height"));
        Assert.NotNull(initialResetKey);
        Assert.NotEqual(initialResetKey, cut.FindComponent<FileZoomPanFrame>().Instance.ResetKey);
    }

    [Fact]
    public void ZoomPanOptions_FlowToTheBuiltInFrame()
    {
        SetupModules();
        var options = new FileObjectViewZoomPanOptions
        {
            ShowControls = false,
            WheelMode = FileZoomPanWheelMode.ControlKey,
            MaximumZoom = 4,
            ZoomFactor = 1.5
        };

        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, CreateContext("photo.png", "image/png", [1]))
            .Add(component => component.Kind, FileObjectViewKind.Image)
            .Add(component => component.ZoomPan, options));

        var frame = cut.FindComponent<FileZoomPanFrame>().Instance;
        Assert.False(frame.ShowControls);
        Assert.Equal(FileZoomPanWheelMode.ControlKey, frame.WheelMode);
        Assert.Equal(4d, frame.MaximumZoom);
        Assert.Equal(1.5d, frame.ZoomFactor);
        Assert.Empty(cut.FindAll("[role='toolbar']"));
    }

    [Fact]
    public void ZoomPan_IsIgnoredForTheBrowserNativePdfViewer()
    {
        SetupModules();

        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, CreateContext("brief.pdf", "application/pdf", [1]))
            .Add(component => component.Kind, FileObjectViewKind.Pdf)
            .Add(component => component.ZoomPan, FileObjectViewZoomPanOptions.Default));

        Assert.Empty(cut.FindComponents<FileZoomPanFrame>());
        Assert.NotNull(cut.Find("[data-testid='interaction-pdf-view'] > object"));
        Assert.DoesNotContain("cdi-ft-object-surface--zoom-pan", cut.Find("[data-testid='interaction-pdf-view']").ClassName);
    }

    [Fact]
    public void FileObjectView_WithoutZoomPanRendersTheTargetDirectly()
    {
        SetupModules();

        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, CreateContext("photo.png", "image/png", [1]))
            .Add(component => component.Kind, FileObjectViewKind.Image));

        Assert.Empty(cut.FindComponents<FileZoomPanFrame>());
        Assert.NotNull(cut.Find("[data-testid='interaction-image-view'] > img"));
    }

    [Fact]
    public void CustomTargetFrame_TakesPrecedenceOverTheBuiltInZoomPan()
    {
        SetupModules();
        RenderFragment<FileObjectViewTargetFrameContext> targetFrame = context => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "target-frame");
            builder.AddContent(2, context.TargetContent);
            builder.CloseElement();
        };

        var cut = Render<FileObjectView>(parameters => parameters
            .Add(component => component.Context, CreateContext("photo.png", "image/png", [1]))
            .Add(component => component.Kind, FileObjectViewKind.Image)
            .Add(component => component.ZoomPan, FileObjectViewZoomPanOptions.Default)
            .Add(component => component.TargetFrame, targetFrame));

        Assert.Empty(cut.FindComponents<FileZoomPanFrame>());
        Assert.NotNull(cut.Find("[data-testid='target-frame'] > img"));
        Assert.DoesNotContain("cdi-ft-object-surface--zoom-pan", cut.Find("[data-testid='interaction-image-view']").ClassName);
    }

    [Fact]
    public void BuiltIns_RegisterZoomPanViewersForRasterImagesAndSvg()
    {
        var composition = FileInteractionComponentComposition.BuiltIn;

        Assert.Equal(
            typeof(ImageFileView),
            composition.Renderers.Resolve(FileInteractionBuiltInProfileIds.Image, FileInteractionMode.View).Renderer?.ComponentType);
        Assert.Equal(
            typeof(SvgFileView),
            composition.Renderers.Resolve(FileInteractionBuiltInProfileIds.Svg, FileInteractionMode.View).Renderer?.ComponentType);
        Assert.Equal(
            typeof(ObjectFileView),
            composition.Renderers.Resolve(FileInteractionBuiltInProfileIds.Object, FileInteractionMode.View).Renderer?.ComponentType);
        Assert.Equal(
            typeof(PdfFileView),
            composition.Renderers.Resolve(FileInteractionBuiltInProfileIds.Pdf, FileInteractionMode.View).Renderer?.ComponentType);
    }

    private void SetupModules()
    {
        var objectUrls = JSInterop.SetupModule(FileObjectUrlInterop.ModulePath);
        objectUrls.SetupVoid(FileObjectUrlInterop.ApplyMethod, _ => true).SetVoidResult();
        objectUrls.SetupVoid(FileObjectUrlInterop.RevokeMethod, _ => true).SetVoidResult();
        var zoomPan = JSInterop.SetupModule(FileZoomPanFrameInterop.ModulePath);
        zoomPan.SetupVoid(FileZoomPanFrameInterop.InitializeMethod, _ => true).SetVoidResult();
        zoomPan.SetupVoid(FileZoomPanFrameInterop.ConfigureMethod, _ => true).SetVoidResult();
        zoomPan.SetupVoid(FileZoomPanFrameInterop.ResetMethod, _ => true).SetVoidResult();
        zoomPan.SetupVoid(FileZoomPanFrameInterop.DestroyMethod, _ => true).SetVoidResult();
    }

    private static FileInteractionRenderContext CreateContext(
        string fileName,
        string mediaType,
        byte[] content,
        long editRevision = 0)
    {
        var request = new FileInteractionRequest(
            new FileReference("test", fileName),
            fileName,
            mediaType: mediaType,
            contentRevision: new FileContentRevision("r1"));
        return new FileInteractionRenderContext(
            request,
            FileInteractionMode.View,
            content,
            editRevision,
            mediaType);
    }
}
