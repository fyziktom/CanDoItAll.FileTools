using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Spreadsheet.Components;

namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet;

public static class FileInteractionSpreadsheetProfileIds
{
    public const string Spreadsheet = "spreadsheet";
}

public static class FileInteractionSpreadsheetExtensions
{
    public const string XlsxMediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static FileInteractionComponentBuilder AddSpreadsheet(
        this FileInteractionComponentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddProfile(new FileInteractionProfileDescriptor(
                FileInteractionSpreadsheetProfileIds.Spreadsheet,
                FileInteractionCapabilities.View,
                extensions: [".xlsx"],
                mediaTypes: [XlsxMediaType],
                priority: 100))
            .AddRenderer(new FileInteractionRendererDescriptor(
                "spreadsheet-view",
                FileInteractionSpreadsheetProfileIds.Spreadsheet,
                FileInteractionMode.View,
                typeof(SpreadsheetFileView),
                FileInteractionContentKind.Binary,
                contentRequirement: FileInteractionContentRequirement.FullContent));
    }
}
