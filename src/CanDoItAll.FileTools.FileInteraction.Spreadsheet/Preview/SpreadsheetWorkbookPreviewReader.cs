using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Xml;
using ClosedXML.Excel;
using ClosedXML.Graphics;

namespace CanDoItAll.FileTools.FileInteraction.Spreadsheet;

public sealed class SpreadsheetWorkbookPreviewReader
{
    private const string FallbackFontResourceName = "ClosedXML.Graphics.Fonts.CarlitoBare-Regular.ttf";

    private static readonly Lazy<IXLGraphicEngine> PortableGraphicEngine =
        new(CreatePortableGraphicEngine, LazyThreadSafetyMode.ExecutionAndPublication);

    public SpreadsheetWorkbookPreview Read(SpreadsheetWorkbookPreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkbookName);
        ValidateLimit(request.MaxWorksheets, SpreadsheetWorkbookPreviewRequest.MaximumWorksheets, nameof(request.MaxWorksheets));
        ValidateLimit(request.MaxRows, SpreadsheetWorkbookPreviewRequest.MaximumRows, nameof(request.MaxRows));
        ValidateLimit(request.MaxColumns, SpreadsheetWorkbookPreviewRequest.MaximumColumns, nameof(request.MaxColumns));

        if (request.Content.IsEmpty)
        {
            throw new InvalidDataException($"Spreadsheet workbook '{request.WorkbookName}' is empty.");
        }

        if (request.Content.Length > SpreadsheetWorkbookPreviewRequest.MaximumContentBytes)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{request.WorkbookName}' exceeds the bounded preview size.");
        }

        ValidateArchive(request.WorkbookName, request.Content);
        using XLWorkbook workbook = OpenWorkbook(request.WorkbookName, request.Content);
        int totalWorksheetCount = workbook.Worksheets.Count;
        SpreadsheetWorksheetPreview[] worksheets = workbook.Worksheets
            .Take(request.MaxWorksheets)
            .Select((worksheet, index) => CreateWorksheetPreview(
                worksheet,
                index + 1,
                request.MaxRows,
                request.MaxColumns))
            .ToArray();

        return new SpreadsheetWorkbookPreview(
            request.WorkbookName,
            totalWorksheetCount,
            worksheets,
            WorksheetsTruncated: totalWorksheetCount > worksheets.Length);
    }

    private static XLWorkbook OpenWorkbook(string workbookName, ReadOnlyMemory<byte> content)
    {
        try
        {
            using Stream stream = OpenContentStream(content);
            return new XLWorkbook(stream, new LoadOptions
            {
                RecalculateAllFormulas = false,
                GraphicEngine = PortableGraphicEngine.Value
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{workbookName}' could not be opened as an XLSX workbook.",
                exception);
        }
    }

    private static void ValidateArchive(string workbookName, ReadOnlyMemory<byte> content)
    {
        try
        {
            using Stream stream = OpenContentStream(content);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count > SpreadsheetWorkbookPreviewRequest.MaximumArchiveEntries)
            {
                throw new InvalidDataException(
                    $"Spreadsheet workbook '{workbookName}' contains too many package entries.");
            }

            var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long expandedBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string entryName = NormalizeEntryName(entry.FullName);
                if (!entryNames.Add(entryName))
                {
                    throw new InvalidDataException(
                        $"Spreadsheet workbook '{workbookName}' contains duplicate package entries.");
                }

                long entryBytes = DrainEntry(workbookName, entry, ref expandedBytes);
                if (IsXmlPart(entryName))
                {
                    EnsureWithinLimit(
                        workbookName,
                        entryBytes,
                        SpreadsheetWorkbookPreviewRequest.MaximumXmlPartBytes,
                        "XML part");
                }
            }

            ValidatePackageComplexity(workbookName, archive);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{workbookName}' is not a valid bounded XLSX archive.",
                exception);
        }
    }

    private static void ValidatePackageComplexity(string workbookName, ZipArchive archive)
    {
        var worksheetCount = 0;
        var cellCount = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string entryName = NormalizeEntryName(entry.FullName);
            PackagePartKind partKind = ResolvePartKind(entryName);
            if (partKind is PackagePartKind.None)
            {
                continue;
            }

            using Stream stream = entry.Open();
            using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });
            while (reader.Read())
            {
                if (reader.NodeType is not XmlNodeType.Element)
                {
                    continue;
                }

                if (partKind is PackagePartKind.Workbook && reader.LocalName == "sheet")
                {
                    worksheetCount++;
                    EnsureWithinLimit(
                        workbookName,
                        worksheetCount,
                        SpreadsheetWorkbookPreviewRequest.MaximumPackageWorksheets,
                        "worksheets");
                }
                else if (partKind is PackagePartKind.Worksheet && reader.LocalName == "c")
                {
                    cellCount++;
                    EnsureWithinLimit(
                        workbookName,
                        cellCount,
                        SpreadsheetWorkbookPreviewRequest.MaximumPackageCells,
                        "cells");
                }
            }
        }
    }

    private static SpreadsheetWorksheetPreview CreateWorksheetPreview(
        IXLWorksheet worksheet,
        int position,
        int maxRows,
        int maxColumns)
    {
        IXLRange? usedRange = worksheet.RangeUsed();
        if (usedRange is null)
        {
            return new SpreadsheetWorksheetPreview(
                worksheet.Name,
                position,
                UsedRangeAddress: string.Empty,
                UsedRowCount: 0,
                UsedColumnCount: 0,
                Values: [],
                RowsTruncated: false,
                ColumnsTruncated: false);
        }

        int usedRowCount = usedRange.RowCount();
        int usedColumnCount = usedRange.ColumnCount();
        int previewRowCount = Math.Min(usedRowCount, maxRows);
        int previewColumnCount = Math.Min(usedColumnCount, maxColumns);
        var values = new List<IReadOnlyList<string>>(previewRowCount);

        for (var rowIndex = 1; rowIndex <= previewRowCount; rowIndex++)
        {
            var row = new List<string>(previewColumnCount);
            for (var columnIndex = 1; columnIndex <= previewColumnCount; columnIndex++)
            {
                row.Add(CellToString(usedRange.Cell(rowIndex, columnIndex)));
            }

            values.Add(row);
        }

        return new SpreadsheetWorksheetPreview(
            worksheet.Name,
            position,
            usedRange.RangeAddress.ToStringRelative(),
            usedRowCount,
            usedColumnCount,
            values,
            RowsTruncated: usedRowCount > previewRowCount,
            ColumnsTruncated: usedColumnCount > previewColumnCount);
    }

    private static string CellToString(IXLCell cell)
    {
        if (cell.HasFormula)
        {
            return "=" + cell.FormulaA1;
        }

        if (cell.IsEmpty())
        {
            return string.Empty;
        }

        return cell.Value.Type switch
        {
            XLDataType.Blank => string.Empty,
            XLDataType.Boolean => cell.GetBoolean().ToString(CultureInfo.InvariantCulture),
            XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => cell.GetDateTime().ToString("O", CultureInfo.InvariantCulture),
            XLDataType.TimeSpan => cell.GetTimeSpan().ToString(),
            _ => cell.GetFormattedString()
        };
    }

    private static Stream OpenContentStream(ReadOnlyMemory<byte> content)
    {
        if (MemoryMarshal.TryGetArray(content, out ArraySegment<byte> segment))
        {
            return new MemoryStream(
                segment.Array!,
                segment.Offset,
                segment.Count,
                writable: false,
                publiclyVisible: false);
        }

        return new MemoryStream(content.ToArray(), writable: false);
    }

    private static long DrainEntry(
        string workbookName,
        ZipArchiveEntry entry,
        ref long totalExpandedBytes)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            using Stream stream = entry.Open();
            long entryBytes = 0;
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                entryBytes = checked(entryBytes + bytesRead);
                totalExpandedBytes = checked(totalExpandedBytes + bytesRead);
                EnsureWithinLimit(
                    workbookName,
                    totalExpandedBytes,
                    SpreadsheetWorkbookPreviewRequest.MaximumExpandedBytes,
                    "expanded package");
            }

            return entryBytes;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static IXLGraphicEngine CreatePortableGraphicEngine()
    {
        using Stream fallbackFont = typeof(DefaultGraphicEngine).Assembly.GetManifestResourceStream(
            FallbackFontResourceName) ?? throw new InvalidOperationException(
            $"ClosedXML fallback font resource '{FallbackFontResourceName}' was not found.");
        return DefaultGraphicEngine.CreateOnlyWithFonts(fallbackFont);
    }

    private static PackagePartKind ResolvePartKind(string entryName)
    {
        if (entryName.Equals("xl/workbook.xml", StringComparison.OrdinalIgnoreCase))
        {
            return PackagePartKind.Workbook;
        }

        return IsWorksheetPart(entryName)
            ? PackagePartKind.Worksheet
            : PackagePartKind.None;
    }

    private static bool IsWorksheetPart(string entryName)
        => entryName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
            && entryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsXmlPart(string entryName)
        => entryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || entryName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEntryName(string entryName)
        => entryName.Replace('\\', '/').TrimStart('/');

    private static void ValidateLimit(int value, int maximum, string parameterName)
    {
        if (value < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"Value must be between 1 and {maximum}.");
        }
    }

    private static void EnsureWithinLimit(
        string workbookName,
        long actual,
        long maximum,
        string subject)
    {
        if (actual > maximum)
        {
            throw new InvalidDataException(
                $"Spreadsheet workbook '{workbookName}' exceeds the bounded {subject} limit.");
        }
    }

    private enum PackagePartKind
    {
        None,
        Workbook,
        Worksheet
    }
}
