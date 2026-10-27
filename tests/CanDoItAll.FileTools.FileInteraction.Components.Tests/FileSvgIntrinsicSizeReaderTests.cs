using System.Text;
using CanDoItAll.FileTools.FileInteraction.Components;

namespace CanDoItAll.FileTools.FileInteraction.Components.Tests;

public sealed class FileSvgIntrinsicSizeReaderTests
{
    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\"></svg>", 800, 600)]
    [InlineData("<svg width=\"800px\" height=\"600PX\"/>", 800, 600)]
    [InlineData("<svg width=\"2in\" height=\"1.5in\"/>", 192, 144)]
    [InlineData("<svg width=\"72pt\" height=\"36pt\"/>", 96, 48)]
    [InlineData("<svg viewBox=\"0 0 1200 800\"/>", 1200, 800)]
    [InlineData("<svg viewBox=\"10,20,300,150\"/>", 300, 150)]
    [InlineData("<svg width=\"100%\" height=\"100%\" viewBox=\"0 0 300 150\"/>", 300, 150)]
    [InlineData("<svg width=\"600\" viewBox=\"0 0 300 150\"/>", 600, 300)]
    [InlineData("<svg height=\"300\" viewBox=\"0 0 4 3\"/>", 400, 300)]
    [InlineData("  \r\n <?xml version=\"1.0\" encoding=\"utf-8\"?><!-- banner --><svg width=\"10\" height=\"20\"/>", 10, 20)]
    [InlineData("<?xml version=\"1.0\"?><!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\"><svg width=\"10\" height=\"20\"/>", 10, 20)]
    [InlineData("<svg:svg xmlns:svg=\"http://www.w3.org/2000/svg\" width=\"5\" height=\"7\"/>", 5, 7)]
    public void TryRead_ResolvesDeclaredSizes(string markup, double expectedWidth, double expectedHeight)
    {
        var size = FileSvgIntrinsicSizeReader.TryRead(Encoding.UTF8.GetBytes(markup));

        Assert.NotNull(size);
        Assert.Equal(expectedWidth, size.Value.Width, precision: 3);
        Assert.Equal(expectedHeight, size.Value.Height, precision: 3);
    }

    [Fact]
    public void TryRead_AcceptsUtf8ByteOrderMark()
    {
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("<svg width=\"30\" height=\"40\"/>"))
            .ToArray();

        var size = FileSvgIntrinsicSizeReader.TryRead(bytes);

        Assert.Equal(new FileSvgIntrinsicSize(30, 40), size);
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain text")]
    [InlineData("<html><svg width=\"1\" height=\"1\"/></html>")]
    [InlineData("<svg/>")]
    [InlineData("<svg width=\"100%\" height=\"100%\"/>")]
    [InlineData("<svg width=\"0\" height=\"5\"/>")]
    [InlineData("<svg width=\"-5\" height=\"5\"/>")]
    [InlineData("<svg width=\"1e9\" height=\"1\"/>")]
    [InlineData("<svg width=\"5\" height=\"5\" viewBox=\"0 0 0 0\"")]
    [InlineData("<svg viewBox=\"0 0 -10 10\"/>")]
    [InlineData("<svg viewBox=\"1 2 3\"/>")]
    [InlineData("<!DOCTYPE svg [<!ENTITY size \"5\">]><svg width=\"&size;\" height=\"5\"/>")]
    public void TryRead_ReturnsNullForUnusableOrUnsafeInput(string markup)
    {
        Assert.Null(FileSvgIntrinsicSizeReader.TryRead(Encoding.UTF8.GetBytes(markup)));
    }

    [Fact]
    public void TryRead_IgnoresBinaryContentWithoutThrowing()
    {
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF, 0x3C];

        Assert.Null(FileSvgIntrinsicSizeReader.TryRead(bytes));
    }

    [Fact]
    public void TryRead_OnlyInspectsBoundedPrefixOfLargeContent()
    {
        var padding = new string(' ', FileSvgIntrinsicSizeReader.MaximumInspectedBytes + 16);
        var lateRoot = Encoding.UTF8.GetBytes($"<!--{padding}--><svg width=\"5\" height=\"5\"/>");

        Assert.Null(FileSvgIntrinsicSizeReader.TryRead(lateRoot));
    }

    [Theory]
    [InlineData("12px", 12)]
    [InlineData("12PT", 16)]
    [InlineData(" 3in ", 288)]
    [InlineData(".5", 0.5)]
    [InlineData("2.54cm", 96)]
    [InlineData("25.4mm", 96)]
    [InlineData("2em", 32)]
    public void ParseLength_ConvertsSupportedUnitsToPixels(string value, double expected)
    {
        var pixels = FileSvgIntrinsicSizeReader.ParseLength(value);

        Assert.NotNull(pixels);
        Assert.Equal(expected, pixels.Value, precision: 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("50%")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("5vw")]
    [InlineData("NaN")]
    public void ParseLength_RejectsRelativeInvalidOrNonPositiveValues(string? value)
    {
        Assert.Null(FileSvgIntrinsicSizeReader.ParseLength(value));
    }

    [Fact]
    public void ParseViewBox_ReadsWidthAndHeightFromMixedSeparators()
    {
        Assert.Equal((320d, 240d), FileSvgIntrinsicSizeReader.ParseViewBox(" 0, 0 \t320,240 "));
        Assert.Null(FileSvgIntrinsicSizeReader.ParseViewBox("0 0 320"));
        Assert.Null(FileSvgIntrinsicSizeReader.ParseViewBox("0 0 320 abc"));
    }
}
