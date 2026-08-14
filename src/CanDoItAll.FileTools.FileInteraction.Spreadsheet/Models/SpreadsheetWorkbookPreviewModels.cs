namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet;

public sealed record SpreadsheetWorkbookPreviewRequest(
    string WorkbookName,
    ReadOnlyMemory<byte> Content,
    int MaxWorksheets = 20,
    int MaxRows = 20,
    int MaxColumns = 12)
{
    public const int MaximumContentBytes = 16 * 1024 * 1024;
    public const int MaximumArchiveEntries = 2048;
    public const long MaximumExpandedBytes = 64L * 1024L * 1024L;
    public const long MaximumXmlPartBytes = 32L * 1024L * 1024L;
    public const int MaximumPackageWorksheets = 256;
    public const int MaximumPackageCells = 250_000;
    public const int MaximumWorksheets = 100;
    public const int MaximumRows = 1000;
    public const int MaximumColumns = 100;
}

public sealed record SpreadsheetWorksheetPreview(
    string Name,
    int Position,
    string UsedRangeAddress,
    int UsedRowCount,
    int UsedColumnCount,
    IReadOnlyList<IReadOnlyList<string>> Values,
    bool RowsTruncated,
    bool ColumnsTruncated)
{
    public bool IsTruncated => RowsTruncated || ColumnsTruncated;
}

public sealed record SpreadsheetWorkbookPreview(
    string DisplayName,
    int TotalWorksheetCount,
    IReadOnlyList<SpreadsheetWorksheetPreview> Worksheets,
    bool WorksheetsTruncated)
{
    public bool IsTruncated => WorksheetsTruncated || Worksheets.Any(worksheet => worksheet.IsTruncated);
}
