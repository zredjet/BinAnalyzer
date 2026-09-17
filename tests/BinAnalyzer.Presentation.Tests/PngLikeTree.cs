using BinAnalyzer.Core.Decoded;

namespace BinAnalyzer.Presentation.Tests;

/// <summary>45 バイトの最小 PNG 風ツリー（signature + IHDR chunk + IEND chunk）。</summary>
internal static class PngLikeTree
{
    public static readonly byte[] Data = BuildData();

    private static byte[] BuildData()
    {
        var d = new byte[45];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(d, 0);
        d[11] = 13; "IHDR"u8.CopyTo(d.AsSpan(12)); d[19] = 1; d[23] = 1; d[24] = 8; d[25] = 2;
        d[29] = 0x90; d[30] = 0x77; d[31] = 0x53; d[32] = 0xDE;
        "IEND"u8.CopyTo(d.AsSpan(37)); d[41] = 0xAE; d[42] = 0x42; d[43] = 0x60; d[44] = 0x82;
        return d;
    }

    public static DecodedStruct Build()
    {
        var ihdr = new DecodedStruct
        {
            Name = "data", StructType = "ihdr", Offset = 16, Size = 13,
            Children =
            [
                new DecodedInteger { Name = "width", Offset = 16, Size = 4, Value = 1 },
                new DecodedInteger { Name = "height", Offset = 20, Size = 4, Value = 1 },
                new DecodedInteger { Name = "bit_depth", Offset = 24, Size = 1, Value = 8 },
                new DecodedInteger { Name = "color_type", Offset = 25, Size = 1, Value = 2, EnumLabel = "truecolor" },
                new DecodedInteger { Name = "compression", Offset = 26, Size = 1, Value = 0 },
                new DecodedInteger { Name = "filter", Offset = 27, Size = 1, Value = 0 },
                new DecodedInteger { Name = "interlace", Offset = 28, Size = 1, Value = 0 },
            ],
        };
        var chunk0 = new DecodedStruct
        {
            Name = "chunk", StructType = "chunk", Offset = 8, Size = 25,
            Children =
            [
                new DecodedInteger { Name = "length", Offset = 8, Size = 4, Value = 13 },
                new DecodedString { Name = "type", Offset = 12, Size = 4, Value = "IHDR", Encoding = "ASCII", Flags = [] },
                ihdr,
                new DecodedInteger { Name = "crc", Offset = 29, Size = 4, Value = 0x907753DE, ChecksumAlgorithm = "crc32", ChecksumValid = true },
            ],
        };
        var chunk1 = new DecodedStruct
        {
            Name = "chunk", StructType = "chunk", Offset = 33, Size = 12,
            Children =
            [
                new DecodedInteger { Name = "length", Offset = 33, Size = 4, Value = 0 },
                new DecodedString { Name = "type", Offset = 37, Size = 4, Value = "IEND", Encoding = "ASCII", Flags = [] },
                new DecodedInteger { Name = "crc", Offset = 41, Size = 4, Value = 0xAE426082, ChecksumAlgorithm = "crc32", ChecksumValid = false },
            ],
        };
        return new DecodedStruct
        {
            Name = "PNG", StructType = "png", Offset = 0, Size = 45,
            Children =
            [
                new DecodedBytes { Name = "signature", Offset = 0, Size = 8, RawBytes = Data.AsMemory(0, 8), ValidationPassed = true },
                new DecodedArray { Name = "chunks", Offset = 8, Size = 37, Elements = [chunk0, chunk1] },
            ],
        };
    }
}
