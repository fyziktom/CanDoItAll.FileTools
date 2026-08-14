using Bunit;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Spreadsheet.Components;
using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet.Tests;

public sealed class SpreadsheetFileViewTests : FileToolsBunitContext
{
    public SpreadsheetFileViewTests()
    {
        Services.AddLogging();
    }

    [Fact]
    public void MultipleWorksheets_RenderAsTabsWithOneActiveGrid()
    {
        IRenderedComponent<SpreadsheetFileView> cut = Render<SpreadsheetFileView>(parameters => parameters
            .Add(component => component.Context, CreateContext(CreateWorkbook())));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(4, cut.FindAll("[role='tab']").Count);
            Assert.Single(cut.FindAll("[data-testid='spreadsheet-grid']"));
            Assert.Contains("Summary marker", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Revenue marker", cut.Markup, StringComparison.Ordinal);
        });

        IReadOnlyList<AngleSharp.Dom.IElement> tabs = cut.FindAll("[role='tab']");
        Assert.All(tabs, tab => Assert.Equal("0", tab.GetAttribute("tabindex")));
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("false", tabs[1].GetAttribute("aria-selected"));

        tabs[1].Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[data-testid='spreadsheet-grid']"));
            Assert.Contains("Revenue marker", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Summary marker", cut.Markup, StringComparison.Ordinal);
            Assert.Equal("false", cut.FindAll("[role='tab']")[0].GetAttribute("aria-selected"));
            Assert.Equal("true", cut.FindAll("[role='tab']")[1].GetAttribute("aria-selected"));
        });
    }

    [Fact]
    public void InvalidWorkbook_RendersSanitizedFailureState()
    {
        IRenderedComponent<SpreadsheetFileView> cut = Render<SpreadsheetFileView>(parameters => parameters
            .Add(component => component.Context, CreateContext([1, 2, 3, 4])));

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='spreadsheet-preview-unavailable']"));
            Assert.DoesNotContain("ZipArchive", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Reader_PreservesFormulaTextAndWorksheetOrder()
    {
        var reader = new SpreadsheetWorkbookPreviewReader();

        SpreadsheetWorkbookPreview result = reader.Read(new SpreadsheetWorkbookPreviewRequest(
            "four-sheets.xlsx",
            CreateWorkbook()));

        Assert.Equal(4, result.TotalWorksheetCount);
        Assert.Equal(["Summary", "Revenue", "Expenses", "Forecast"], result.Worksheets.Select(sheet => sheet.Name));
        Assert.Equal("=SUM(B1:B2)", result.Worksheets[0].Values[2][1]);
    }

    private static FileInteractionRenderContext CreateContext(byte[] content)
        => new(
            new FileInteractionRequest(
                new FileReference("test", "four-sheets.xlsx"),
                "four-sheets.xlsx",
                mediaType: FileInteractionSpreadsheetExtensions.XlsxMediaType),
            FileInteractionMode.View,
            content,
            editRevision: 0,
            mediaType: FileInteractionSpreadsheetExtensions.XlsxMediaType);

    private static byte[] CreateWorkbook()
    {
        using var workbook = new XLWorkbook();
        AddWorksheet(workbook, "Summary", "Summary marker");
        AddWorksheet(workbook, "Revenue", "Revenue marker");
        AddWorksheet(workbook, "Expenses", "Expenses marker");
        AddWorksheet(workbook, "Forecast", "Forecast marker");
        workbook.Worksheet("Summary").Cell("B3").FormulaA1 = "SUM(B1:B2)";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddWorksheet(XLWorkbook workbook, string name, string marker)
    {
        IXLWorksheet worksheet = workbook.Worksheets.Add(name);
        worksheet.Cell("A1").Value = marker;
        worksheet.Cell("B1").Value = 10;
        worksheet.Cell("B2").Value = 20;
    }
}
