using System.Collections.Frozen;
using ClosedXML.Parser;
using References = System.Collections.Generic.List<CanDoItAll.FileTools.FileInteraction.Spreadsheet.PreviewFormulaReference>;
using Shape = CanDoItAll.FileTools.FileInteraction.Spreadsheet.PreviewFormulaShape;

namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet;

internal sealed class PreviewFormulaPlan : IAstFactory<bool, Shape, References> {
    private static readonly PreviewFormulaPlan Factory = new();
    private static readonly FrozenSet<string> Functions = new[] {
        "ABS", "AND", "AVERAGE", "CEILING", "COLUMN", "COLUMNS",
        "CONCAT", "CONCATENATE", "COUNT", "COUNTA", "COUNTBLANK", "COUNTIF", "COUNTIFS", "DATE", "DAY",
        "DAYS", "EDATE", "EOMONTH", "EXACT", "FALSE", "FIND", "FLOOR", "FV", "HLOOKUP", "HOUR", "IF",
        "IFERROR", "INDEX", "INT", "IPMT", "ISBLANK", "ISERROR", "ISNA", "ISNUMBER", "ISTEXT", "LEFT",
        "LEN", "LOWER", "MATCH", "MAX", "MEDIAN", "MID", "MIN", "MOD", "MONTH", "NA", "NOT", "NOW", "OR", "PMT", "POWER", "PRODUCT",
        "RIGHT", "ROUND", "ROUNDDOWN", "ROUNDUP", "ROW", "ROWS", "SEARCH", "SQRT", "SUBSTITUTE",
        "STDEV.P", "STDEV.S", "SUM", "SUMIF", "SUMIFS", "SUMPRODUCT", "TEXT", "TEXTJOIN", "TIME", "TODAY", "TRIM", "TRUE", "UPPER",
        "VALUE", "VLOOKUP", "WEEKDAY", "YEAR"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> SparseAggregates = new[] {
        "SUM", "AVERAGE", "COUNT", "COUNTA", "COUNTBLANK", "MIN", "MAX"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static Shape Read(string formula, References references)
        => FormulaParser<bool, Shape, References>.CellFormulaA1(formula, references, Factory);

    public Shape Reference(References refs, SymbolRange range, ReferenceArea reference)
        => SheetReference(refs, range, string.Empty, reference);

    public Shape SheetReference(References refs, SymbolRange range, string sheet, ReferenceArea reference) {
        refs.Add(new(sheet, reference));
        return Shape.Reference;
    }

    public Shape Function(References refs, SymbolRange range, ReadOnlySpan<char> name, IReadOnlyList<Shape> arguments) {
        const string futurePrefix = "_xlfn.";
        if (name.StartsWith(futurePrefix, StringComparison.OrdinalIgnoreCase)) {
            name = name[futurePrefix.Length..];
        }

        string function = name.ToString();
        if (!Functions.Contains(function) || arguments.Contains(Shape.Unsupported)) {
            return Shape.Unsupported;
        }

        return SparseAggregates.Contains(function) && arguments.All(argument => argument == Shape.Reference)
            ? Shape.SparseAggregate
            : Shape.Scalar;
    }

    public Shape BinaryNode(References refs, SymbolRange range, BinaryOperation op, Shape left, Shape right)
        => left != Shape.Unsupported && right != Shape.Unsupported && op is not (BinaryOperation.Range or BinaryOperation.Union or BinaryOperation.Intersection)
            ? Shape.Scalar : Shape.Unsupported;
    public Shape Unary(References refs, SymbolRange range, UnaryOperation op, Shape value)
        => value != Shape.Unsupported && op is not UnaryOperation.SpillRange ? Shape.Scalar : Shape.Unsupported;
    public Shape Nested(References refs, SymbolRange range, Shape value) => value;
    public Shape BlankNode(References refs, SymbolRange range) => Shape.Scalar;
    public Shape LogicalNode(References refs, SymbolRange range, bool value) => Shape.Scalar;
    public Shape ErrorNode(References refs, SymbolRange range, ReadOnlySpan<char> error) => Shape.Scalar;
    public Shape NumberNode(References refs, SymbolRange range, double value) => Shape.Scalar;
    public Shape TextNode(References refs, SymbolRange range, string text) => Shape.Scalar;

    // Other Excel constructs, including external references, require a saved result.
    public Shape Name(References refs, SymbolRange range, string name) => Shape.Unsupported;
    public Shape SheetName(References refs, SymbolRange range, string sheet, string name) => Shape.Unsupported;
    public Shape BangName(References refs, SymbolRange range, string name) => Shape.Unsupported;
    public Shape BangReference(References refs, SymbolRange range, ReferenceArea reference) => Shape.Unsupported;
    public Shape Reference3D(References refs, SymbolRange range, string first, string last, ReferenceArea reference) => Shape.Unsupported;
    public Shape ExternalSheetReference(References refs, SymbolRange range, int book, string sheet, ReferenceArea reference) => Shape.Unsupported;
    public Shape ExternalReference3D(References refs, SymbolRange range, int book, string first, string last, ReferenceArea reference) => Shape.Unsupported;
    public Shape ExternalName(References refs, SymbolRange range, int book, string name) => Shape.Unsupported;
    public Shape ExternalSheetName(References refs, SymbolRange range, int book, string sheet, string name) => Shape.Unsupported;
    public Shape StructureReference(References refs, SymbolRange range, StructuredReferenceArea area, string? first, string? last) => Shape.Unsupported;
    public Shape StructureReference(References refs, SymbolRange range, string table, StructuredReferenceArea area, string? first, string? last) => Shape.Unsupported;
    public Shape ExternalStructureReference(References refs, SymbolRange range, int book, string table, StructuredReferenceArea area, string? first, string? last) => Shape.Unsupported;
    public Shape CellFunction(References refs, SymbolRange range, RowCol cell, IReadOnlyList<Shape> args) => Shape.Unsupported;
    public Shape Function(References refs, SymbolRange range, string sheet, ReadOnlySpan<char> name, IReadOnlyList<Shape> args) => Shape.Unsupported;
    public Shape ExternalFunction(References refs, SymbolRange range, int book, ReadOnlySpan<char> name, IReadOnlyList<Shape> args) => Shape.Unsupported;
    public Shape ExternalFunction(References refs, SymbolRange range, int book, string sheet, ReadOnlySpan<char> name, IReadOnlyList<Shape> args) => Shape.Unsupported;
    public Shape ArrayNode(References refs, SymbolRange range, int rows, int columns, IReadOnlyList<bool> elements) => Shape.Unsupported;
    public bool LogicalValue(References refs, SymbolRange range, bool value) => false;
    public bool NumberValue(References refs, SymbolRange range, double value) => false;
    public bool TextValue(References refs, SymbolRange range, string text) => false;
    public bool ErrorValue(References refs, SymbolRange range, ReadOnlySpan<char> error) => false;
}

internal readonly record struct PreviewFormulaReference(string Sheet, ReferenceArea Area);

internal enum PreviewFormulaShape { Unsupported, Scalar, Reference, SparseAggregate }
