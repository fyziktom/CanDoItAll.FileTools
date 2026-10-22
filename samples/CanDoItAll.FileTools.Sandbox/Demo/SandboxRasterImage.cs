using System.Buffers.Binary;
using System.IO.Compression;

namespace CanDoItAll.FileTools.Sandbox.Demo;

/// <summary>
/// Generates a deterministic calibration PNG (grid, distinct corner blocks, center ring) so zoom and pan
/// reachability can be proven visually without shipping a binary asset.
/// </summary>
internal static class SandboxRasterImage
{
    private const int CellSize = 100;
    private const int CornerWidth = 220;
    private const int CornerHeight = 140;

    private static readonly uint[] CrcTable = CreateCrcTable();

    public static byte[] CreateCalibrationPng(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, CornerWidth * 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, CornerHeight * 2);

        int stride = 1 + width * 3;
        var raw = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * stride;
            raw[rowStart] = 0;
            for (int x = 0; x < width; x++)
            {
                (byte red, byte green, byte blue) = Pixel(x, y, width, height);
                int offset = rowStart + 1 + x * 3;
                raw[offset] = red;
                raw[offset + 1] = green;
                raw[offset + 2] = blue;
            }
        }

        using var compressed = new MemoryStream();
        using (var deflate = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", compressed.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static (byte Red, byte Green, byte Blue) Pixel(int x, int y, int width, int height)
    {
        bool left = x < CornerWidth;
        bool right = x >= width - CornerWidth;
        bool top = y < CornerHeight;
        bool bottom = y >= height - CornerHeight;
        if (top && left)
        {
            return (0xC8, 0x3A, 0x2F);
        }

        if (top && right)
        {
            return (0x24, 0x59, 0xD3);
        }

        if (bottom && left)
        {
            return (0xD9, 0x8C, 0x1B);
        }

        if (bottom && right)
        {
            return (0x2E, 0x8B, 0x57);
        }

        double centerX = width / 2d;
        double centerY = height / 2d;
        double radius = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
        if (radius is >= 120 and <= 136)
        {
            return (0x1D, 0x1D, 0x1B);
        }

        if (x % CellSize == 0 || y % CellSize == 0 || Math.Abs(x - y) < 2)
        {
            return (0xB9, 0xB3, 0xA6);
        }

        bool lightCell = ((x / CellSize) + (y / CellSize)) % 2 == 0;
        if (lightCell)
        {
            return (0xF4, 0xF1, 0xEA);
        }

        return (0xE6, 0xE1, 0xD6);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        uint crc = Crc32(data, Crc32(typeBytes, 0xFFFFFFFFu, finalize: false), finalize: true);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        stream.Write(crcBytes);
    }

    private static uint Crc32(byte[] data, uint seed, bool finalize)
    {
        uint crc = seed;
        foreach (byte value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return finalize ? crc ^ 0xFFFFFFFFu : crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
