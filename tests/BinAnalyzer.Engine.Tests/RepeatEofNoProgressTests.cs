using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// repeat: eof + エラー継続モードで、末尾の端数バイトが要素として読めず 1 バイトも進まない場合に
/// 無限ループせず打ち切ること（REQ-168 で GUI のエンディアン切替時に発覚）。
/// </summary>
public class RepeatEofNoProgressTests
{
    private const string Yaml = """
        name: Rec
        endianness: big
        root: main
        structs:
          main:
            - name: records
              type: struct
              struct: rec
              repeat: eof
          rec:
            - name: len
              type: uint32
            - name: body
              type: bytes
              size: "{len}"
        """;

    [Fact(Timeout = 5000)]
    public async Task TrailingPartialElement_Terminates_WithTruncation()
    {
        await Task.Yield();
        var format = new YamlFormatLoader().LoadFromString(Yaml);
        // 1 レコード (len=2, body=2B) の後に 1 バイトだけ余る → 2 要素目は uint32 を読めない
        var data = new byte[] { 0, 0, 0, 2, 0xAA, 0xBB, 0xFF };

        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);

        var records = result.Root.Children[0].Should().BeOfType<DecodedArray>().Subject;
        records.Elements.Should().HaveCount(2);
        records.Elements[0].Should().BeOfType<DecodedStruct>();
        records.Truncated.Should().BeTrue();
        records.TruncationReason.Should().Contain("no progress");
        result.Errors.Should().NotBeEmpty();
    }

    [Fact(Timeout = 5000)]
    public async Task HugeLengthThenPartial_Terminates()
    {
        await Task.Yield();
        var format = new YamlFormatLoader().LoadFromString(Yaml);
        // len が巨大 → body でエラー、その後も進めないバイト列
        var data = new byte[] { 0x7F, 0, 0, 0, 1, 2, 3 };

        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);

        var records = result.Root.Children[0].Should().BeOfType<DecodedArray>().Subject;
        records.Truncated.Should().BeTrue();
    }
}
