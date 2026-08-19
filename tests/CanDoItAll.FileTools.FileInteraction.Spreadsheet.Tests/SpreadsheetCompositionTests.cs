using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Spreadsheet.Components;

namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet.Tests;

public sealed class SpreadsheetCompositionTests
{
    [Fact]
    public void AddSpreadsheet_OverridesTheBaseObjectFallbackForXlsx()
    {
        var request = new FileInteractionRequest(
            new FileReference("test", "book.xlsx"),
            "book.xlsx",
            mediaType: FileInteractionSpreadsheetExtensions.XlsxMediaType);
        var baseComposition = new FileInteractionComponentBuilder().AddBuiltIns().Build();
        var spreadsheetComposition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddSpreadsheet()
            .Build();

        Assert.Equal(
            FileInteractionBuiltInProfileIds.Object,
            baseComposition.Core.Profiles.Resolve(request).Profile!.Id);
        Assert.Equal(
            FileInteractionSpreadsheetProfileIds.Spreadsheet,
            spreadsheetComposition.Core.Profiles.Resolve(request).Profile!.Id);
    }

    [Fact]
    public void AddSpreadsheet_RegistersAFullContentReadOnlyRenderer()
    {
        var composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddSpreadsheet()
            .Build();
        FileInteractionProfileDescriptor profile = composition.Core.Profiles.Profiles.Single(
            candidate => candidate.Id == FileInteractionSpreadsheetProfileIds.Spreadsheet);
        FileInteractionRendererDescriptor renderer = composition.Renderers.Resolve(
            profile.Id,
            FileInteractionMode.View).Renderer!;

        Assert.Equal(FileInteractionCapabilities.View, profile.Capabilities);
        Assert.Equal(typeof(SpreadsheetFileView), renderer.ComponentType);
        Assert.Equal(FileInteractionContentKind.Binary, renderer.ContentKind);
        Assert.Equal(FileInteractionContentRequirement.FullContent, renderer.ContentRequirement);
    }
}
