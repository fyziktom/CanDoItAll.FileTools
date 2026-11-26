using System.Globalization;
using ClosedXML.Excel;
using ClosedXML.Parser;

namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet;

internal sealed class PreviewFormulaCalculation(XLWorkbook source, CancellationToken cancellation) : IDisposable {
    private const int MaximumDependencyDepth = 64;
    private const int MaximumFormulaLength = 2048;
    private const int MaximumWork = 4 * XLHelper.MaxRowNumber;
    private const string UnavailableText = "Calculation unavailable";

    // Reading a dirty XLSX cell through Value recalculates the entire source workbook.
    // Evaluate only the preview's precedents against a workbook containing scalar values.
    private XLWorkbook? values;
    private readonly Dictionary<(IXLWorksheet Sheet, IXLAddress Address), CellState> states = [];
    private int remainingWork = MaximumWork;

    internal string FormatCell(IXLCell cell) {
        cancellation.ThrowIfCancellationRequested();
        if (!cell.HasFormula) {
            return cell.GetFormattedString(CultureInfo.InvariantCulture);
        }

        if (!Prepare(cell, 0)) {
            return UnavailableText;
        }

        IXLCell result = ValueCell(cell);
        result.Style.NumberFormat = cell.Style.NumberFormat;
        return result.GetFormattedString(CultureInfo.InvariantCulture);
    }

    private bool Prepare(IXLCell cell, int depth) {
        cancellation.ThrowIfCancellationRequested();
        var address = (cell.Worksheet, cell.Address);
        if (states.TryGetValue(address, out CellState state)) {
            return state == CellState.Available;
        }

        XLCellValue saved = cell.CachedValue;
        XLCellValue? value = saved;
        if (cell.HasFormula && (saved.IsBlank || source.FullCalculationOnLoad || source.ForceFullCalculation)) {
            states[address] = CellState.Calculating;
            XLCellValue? calculated = depth < MaximumDependencyDepth ? Evaluate(cell, depth) : null;
            value = calculated ?? (saved.IsBlank ? (XLCellValue?)null : saved);
        }

        if (value is { } available) {
            // ClosedXML treats a leading apostrophe in assigned text as an Excel quote prefix.
            ValueCell(cell).Value = available.IsText && available.GetText().StartsWith('\'') ? "'" + available.GetText() : available;
            states[address] = CellState.Available;
            return true;
        }

        states[address] = CellState.Unavailable;
        return false;
    }

    private XLCellValue? Evaluate(IXLCell cell, int depth) {
        string formula = cell.FormulaA1;
        if (cell.HasArrayFormula || formula.Length > MaximumFormulaLength || !TrySpend(1)) {
            return null;
        }

        try {
            var references = new List<PreviewFormulaReference>();
            PreviewFormulaShape shape = PreviewFormulaPlan.Read(formula, references);
            if (shape == PreviewFormulaShape.Unsupported) {
                return null;
            }

            foreach (var reference in references) {
                string sheetName = reference.Sheet.Length == 0 ? cell.Worksheet.Name : reference.Sheet;
                if (!source.TryGetWorksheet(sheetName, out IXLWorksheet? sheet)) {
                    continue;
                }

                ValueSheet(sheet.Name);
                IXLRange range = sheet.Range(reference.Area.GetDisplayStringA1());
                long size = (long)range.RowCount() * range.ColumnCount();
                if (shape != PreviewFormulaShape.SparseAggregate && !TrySpend(size)) {
                    return null;
                }

                foreach (IXLCell input in range.CellsUsed(XLCellsUsedOptions.Contents)) {
                    if ((shape == PreviewFormulaShape.SparseAggregate && !TrySpend(1)) || !Prepare(input, depth + 1)) {
                        return null;
                    }
                }
            }

            return ValueSheet(cell.Worksheet.Name).Evaluate(formula, cell.Address.ToStringRelative());
        }
        catch (Exception exception) when (exception is ParsingException or NotSupportedException or NotImplementedException
            or ArgumentException or InvalidOperationException or ArithmeticException or FormatException) {
            return null;
        }
    }

    private bool TrySpend(long amount) {
        if (amount > remainingWork) {
            return false;
        }

        remainingWork -= (int)amount;
        return true;
    }

    private IXLCell ValueCell(IXLCell cell)
        => ValueSheet(cell.Worksheet.Name).Cell(cell.Address.RowNumber, cell.Address.ColumnNumber);

    private IXLWorksheet ValueSheet(string name) {
        values ??= new XLWorkbook { Use1904DateSystem = source.Use1904DateSystem };
        return values.TryGetWorksheet(name, out IXLWorksheet? sheet) ? sheet : values.AddWorksheet(name);
    }

    public void Dispose() => values?.Dispose();

    private enum CellState { Calculating, Available, Unavailable }
}
