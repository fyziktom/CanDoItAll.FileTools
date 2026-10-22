using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace CanDoItAll.FileTools.FileInteraction.Components;

/// <summary>Intrinsic CSS pixel size declared by an SVG document root.</summary>
internal readonly record struct FileSvgIntrinsicSize(double Width, double Height);

/// <summary>
/// Reads the declared size of an SVG root element from bounded, untrusted bytes without resolving DTDs,
/// entities, or external resources. A result is best-effort metadata for viewport fitting only.
/// </summary>
internal static partial class FileSvgIntrinsicSizeReader
{
    internal const int MaximumInspectedBytes = 256 * 1024;
    internal const double MaximumDimension = 100_000;

    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        MaxCharactersFromEntities = 0,
        CloseInput = true
    };

    public static FileSvgIntrinsicSize? TryRead(ReadOnlyMemory<byte> content)
    {
        var start = FindMarkupStart(content.Span);
        if (start < 0)
        {
            return null;
        }

        var inspected = content[start..];
        if (inspected.Length > MaximumInspectedBytes)
        {
            inspected = inspected[..MaximumInspectedBytes];
        }

        try
        {
            using var stream = new MemoryStream(inspected.ToArray(), writable: false);
            using var reader = XmlReader.Create(stream, ReaderSettings);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (!string.Equals(reader.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                return Resolve(
                    ParseLength(reader.GetAttribute("width")),
                    ParseLength(reader.GetAttribute("height")),
                    ParseViewBox(reader.GetAttribute("viewBox")));
            }
        }
        catch (Exception exception) when (exception is XmlException
            or IOException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or DecoderFallbackException)
        {
        }

        return null;
    }

    internal static double? ParseLength(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = LengthPattern().Match(value);
        if (!match.Success)
        {
            return null;
        }

        var unit = match.Groups["unit"].Value.ToLowerInvariant();
        var factor = unit switch
        {
            "" or "px" => 1d,
            "pt" => 96d / 72d,
            "pc" => 16d,
            "in" => 96d,
            "cm" => 96d / 2.54d,
            "mm" => 96d / 25.4d,
            "q" => 96d / 101.6d,
            "em" or "rem" => 16d,
            "ex" => 8d,
            _ => double.NaN
        };
        if (double.IsNaN(factor)
            || !double.TryParse(
                match.Groups["number"].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return null;
        }

        var pixels = number * factor;
        return IsUsable(pixels) ? pixels : null;
    }

    internal static (double Width, double Height)? ParseViewBox(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            return null;
        }

        var numbers = new double[4];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[index])
                || !double.IsFinite(numbers[index]))
            {
                return null;
            }
        }

        return IsUsable(numbers[2]) && IsUsable(numbers[3]) ? (numbers[2], numbers[3]) : null;
    }

    private static FileSvgIntrinsicSize? Resolve(double? width, double? height, (double Width, double Height)? viewBox)
    {
        if (width is { } explicitWidth && height is { } explicitHeight)
        {
            return Create(explicitWidth, explicitHeight);
        }

        if (viewBox is not { } box)
        {
            return null;
        }

        if (width is { } onlyWidth)
        {
            return Create(onlyWidth, onlyWidth * box.Height / box.Width);
        }

        if (height is { } onlyHeight)
        {
            return Create(onlyHeight * box.Width / box.Height, onlyHeight);
        }

        return Create(box.Width, box.Height);
    }

    private static FileSvgIntrinsicSize? Create(double width, double height)
        => IsUsable(width) && IsUsable(height) ? new FileSvgIntrinsicSize(width, height) : null;

    private static bool IsUsable(double value)
        => double.IsFinite(value) && value > 0 && value <= MaximumDimension;

    /// <summary>Returns the offset of the first markup character after any byte order mark and leading whitespace, or -1.</summary>
    private static int FindMarkupStart(ReadOnlySpan<byte> content)
    {
        var offset = 0;
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
        {
            offset = 3;
        }
        else if (content.Length >= 2
            && ((content[0] == 0xFF && content[1] == 0xFE) || (content[0] == 0xFE && content[1] == 0xFF)))
        {
            // UTF-16 documents are left to the XML reader, which needs the byte order mark to select the decoder.
            return content.Length > 2 ? 0 : -1;
        }

        for (var index = offset; index < content.Length; index++)
        {
            var value = content[index];
            if (value == (byte)'<')
            {
                return index;
            }

            if (value is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return -1;
            }
        }

        return -1;
    }

    [GeneratedRegex(
        @"^\s*(?<number>[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)\s*(?<unit>[a-zA-Z%]*)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LengthPattern();
}
