using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class BitfieldOutputTests
{
    [Fact]
    public void TreeOutput_Bitfield_ShowsHierarchy()
    {
        var root = new DecodedStruct
        {
            Name = "test",
            StructType = "test",
            Offset = 0,
            Size = 1,
            Children =
            [
                new DecodedBitfield
                {
                    Name = "flags",
                    Offset = 0,
                    Size = 1,
                    RawValue = 0xA5,
                    Fields =
                    [
                        new BitfieldValue("high", 7, 4, 10, null, null),
                        new BitfieldValue("flag_a", 3, 3, 1, null, null),
                        new BitfieldValue("mode", 1, 0, 1, "normal", null),
                    ],
                },
            ],
        };

        var formatter = new TreeOutputFormatter();
        var output = formatter.Format(root);

        output.Should().Contain("flags [0x00000000] (1 bytes): 0xA5");
        output.Should().Contain("high: 10 (bits 7:4)");
        output.Should().Contain("flag_a: 1 (bit 3)");
        output.Should().Contain("mode: 1 (bits 1:0) \"normal\"");
        output.Should().Contain("└── mode");
    }
}
