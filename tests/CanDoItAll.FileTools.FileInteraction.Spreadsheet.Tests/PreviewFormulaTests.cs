using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet.Tests;

public sealed class PreviewFormulaTests {
    [Theory]
    [InlineData("SUM(Data!B1:B3)", "70")]
    [InlineData("SUM(Data!B:B)", "70")]
    [InlineData("(Data!$B$1+Data!B2)*2-Data!B3/4", "50")]
    [InlineData("ROUND(AVERAGE(Data!B1:B3),2)", "23.33")]
    [InlineData("MAX(Data!B1:B3)-MIN(Data!B1:B3)", "30")]
    [InlineData("COUNT(Data!A1:B3)+COUNTA(Data!A1:A3)", "6")]
    [InlineData("COUNTIF(Data!B1:B3,\">=20\")", "2")]
    [InlineData("COUNTIFS(Data!B1:B3,\">=10\",Data!A1:A3,\"West\")", "2")]
    [InlineData("SUMIFS(Data!B1:B3,Data!A1:A3,\"West\")", "50")]
    [InlineData("IF(AND(Data!B2>10,Data!B2<40),\"On \"&\"plan\",\"Check\")", "On plan")]
    [InlineData("VLOOKUP(\"East\",Data!$A$1:$B$3,2,FALSE)", "20")]
    [InlineData("INDEX(Data!B1:B3,MATCH(\"East\",Data!A1:A3,0))", "20")]
    [InlineData("SUMPRODUCT(Data!B1:B3,Data!B1:B3)", "2100")]
    [InlineData("IFERROR(1/0,42)", "42")]
    [InlineData("1/0", "#DIV/0!")]
    [InlineData("Missing!A1", "#REF!")]
    [InlineData("IFERROR(Missing!A1,42)", "42")]
    [InlineData("ROUND(PMT(0.05/12,60,-10000),2)", "188.71")]
    [InlineData("FV(0,10,-10)", "100")]
    [InlineData("IFERROR(NA(),12)", "12")]
    public void EvaluatesCommonFormulasWithoutSavedValues(string formula, string expected) {
        byte[] bytes = Workbook((book, output) => {
            output.Cell("A1").FormulaA1 = formula;
            IXLWorksheet data = book.AddWorksheet("Data");
            data.Cell("A1").Value = "West";
            data.Cell("A2").Value = "East";
            data.Cell("A3").Value = "West";
            data.Cell("B1").Value = 10;
            data.Cell("B2").Value = 20;
            data.Cell("B3").Value = 40;
        });

        Assert.Equal(expected, Preview(bytes)[0][0]);
    }

    [Fact]
    public void FollowsQuotedForwardReferencesBeyondTheVisibleWindowAndFormatsResults() {
        byte[] bytes = Workbook((book, output) => {
            IXLWorksheet data = book.AddWorksheet("Q1 O'Brien");
            data.Cell("Z120").Value = 1;
            data.Cell("Z121").Value = 3;
            output.Cell("A1").FormulaA1 = "B30";
            output.Cell("A1").Style.NumberFormat.Format = "0.0%";
            output.Cell("B30").FormulaA1 = "'Q1 O''Brien'!$Z$120 / SUM('Q1 O''Brien'!Z120:Z121)";
            output.Cell("B1").FormulaA1 = "DATE(2026,1,2)";
            output.Cell("B1").Style.NumberFormat.Format = "yyyy-mm-dd";
            output.Cell("C1").FormulaA1 = "1234.5";
            output.Cell("C1").Style.NumberFormat.Format = "#,##0.00";
            output.Cell("D1").Value = "=literal text";
            output.Cell("E1").FormulaA1 = "\"'quoted text\"";
            output.Cell("F1").FormulaA1 = "E1";
        });

        Assert.Equal(["25.0%", "2026-01-02", "1,234.50", "=literal text", "'quoted text", "'quoted text"], Preview(bytes)[0]);
    }

    [Fact]
    public void IsolatesUnsupportedFunctionsCircularReferencesAndLargeRanges() {
        byte[] bytes = Workbook((book, output) => {
            output.Cell("A1").FormulaA1 = "B1";
            output.Cell("B1").FormulaA1 = "A1";
            output.Cell("C1").FormulaA1 = "CUBEVALUE(\"x\",\"y\")";
            output.Cell("D1").FormulaA1 = "SUMPRODUCT(Data!A:Z,Data!A:Z)";
            output.Cell("E1").FormulaA1 = "F1+2";
            output.Cell("F1").FormulaA1 = "Data!A1*10";
            output.Cell("G1").FormulaA1 = "1/0";
            book.AddWorksheet("Data").Cell("A1").Value = 4;
        });

        Assert.Equal(["Calculation unavailable", "Calculation unavailable", "Calculation unavailable",
            "Calculation unavailable", "42", "40", "#DIV/0!"], Preview(bytes)[0]);
    }

    [Fact]
    public void WholeColumnTotalsScaleWithPopulatedCells() {
        byte[] bytes = Workbook((book, output) => {
            IXLWorksheet data = book.AddWorksheet("Data");
            data.Cell("B1000").Value = 42;
            for (int column = 1; column <= 12; column++) {
                output.Cell(1, column).FormulaA1 = "SUM(Data!B:B)";
            }
        });

        Assert.All(Preview(bytes)[0], cell => Assert.Equal("42", cell));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FormatsDatesAndEvaluatesReferencesInBothWorkbookDateSystems(bool use1904) {
        byte[] bytes = Workbook((book, sheet) => {
            book.Use1904DateSystem = use1904;
            sheet.Cell("A1").Value = new DateTime(2026, 1, 2);
            sheet.Cell("A1").Style.NumberFormat.Format = "yyyy-mm-dd";
            sheet.Cell("B1").FormulaA1 = "A1+1";
            sheet.Cell("B1").Style.NumberFormat.Format = "yyyy-mm-dd";
            sheet.Cell("C1").FormulaA1 = "YEAR(A1)";
        });

        Assert.Equal(["2026-01-02", "2026-01-03", "2026"], Preview(bytes)[0]);
    }

    [Theory]
    [InlineData(false, "0")]
    [InlineData(true, "42")]
    public void HonorsSavedResultsAndWorkbookRecalculationRequests(bool recalculate, string expected) {
        byte[] input = Workbook((_, sheet) => {
            sheet.Cell("A1").FormulaA1 = "6*7";
            sheet.Cell("B1").FormulaA1 = "CUBEVALUE(\"x\",\"y\")";
            sheet.Cell("B1").Style.NumberFormat.Format = "0.0%";
            sheet.Cell("C1").FormulaA1 = "B1*100";
        });
        using var stream = new MemoryStream();
        stream.Write(input);
        stream.Position = 0;
        using (var file = SpreadsheetDocument.Open(stream, true)) {
            WorkbookPart part = file.WorkbookPart!;
            CalculationProperties calculation = part.Workbook.CalculationProperties ??= new();
            calculation.FullCalculationOnLoad = recalculate;
            calculation.ForceFullCalculation = false;
            Cell[] cells = part.WorksheetParts.Single().Worksheet.Descendants<Cell>().ToArray();
            cells[0].CellValue = new CellValue("0");
            cells[1].CellValue = new CellValue("0.25");
        }

        byte[] bytes = stream.ToArray();
        Assert.Equal([expected, "25.0%", "25"], Preview(bytes)[0]);
        Assert.Equal(stream.ToArray(), bytes);
    }

    [Fact]
    public void KeepsTextLogicalAndErrorResultsFromSavedFormulaCells() {
        byte[] input = Workbook((_, sheet) => {
            sheet.Cell("A1").FormulaA1 = "CUBEVALUE(\"x\",\"y\")";
            sheet.Cell("B1").FormulaA1 = "CUBEVALUE(\"x\",\"y\")";
            sheet.Cell("C1").FormulaA1 = "CUBEVALUE(\"x\",\"y\")";
            sheet.Cell("D1").FormulaA1 = "A1&IF(B1,\" yes\",\" no\")";
        });
        using var stream = new MemoryStream();
        stream.Write(input);
        stream.Position = 0;
        using (var file = SpreadsheetDocument.Open(stream, true)) {
            Cell[] cells = file.WorkbookPart!.WorksheetParts.Single().Worksheet.Descendants<Cell>().ToArray();
            cells[0].CellFormula!.Text = "'[1]Rates'!A1";
            cells[0].DataType = CellValues.String;
            cells[0].CellValue = new CellValue("saved value");
            cells[1].DataType = CellValues.Boolean;
            cells[1].CellValue = new CellValue("0");
            cells[2].DataType = CellValues.Error;
            cells[2].CellValue = new CellValue("#N/A");
        }

        Assert.Equal(["saved value", "FALSE", "#N/A", "saved value no"], Preview(stream.ToArray())[0]);
    }

    private static IReadOnlyList<IReadOnlyList<string>> Preview(byte[] bytes)
        => new SpreadsheetWorkbookPreviewReader().Read(new("preview.xlsx", bytes, MaxWorksheets: 1, MaxRows: 1)).Worksheets[0].Values;

    private static byte[] Workbook(Action<XLWorkbook, IXLWorksheet> configure) {
        using var book = new XLWorkbook();
        configure(book, book.AddWorksheet("Output"));
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }
}
