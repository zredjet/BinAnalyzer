using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-160 のファズテストで見つかったエンジンの不具合の回帰テスト。各テストが 1 つの修正に対応する。
/// </summary>
public class FuzzRegressionTests
{
    private static FormatDefinition Load(string yaml) => new YamlFormatLoader().LoadFromString(yaml);

    // ---------- 1. フィールド単位の endianness が変数の束縛を捨てていた（ICO の image_magic） ----------

    private const string FieldEndianYaml = """
        name: T
        endianness: little
        root: main
        structs:
          main:
            - name: tag
              type: uint16
              endianness: big
            - name: body
              type: switch
              switch_on: "{tag}"
              cases:
                "0x0102": one
              default: other
          one:
            - name: x
              type: uint8
          other:
            - name: y
              type: uint8
        """;

    [Fact]
    public void FieldLevelEndianness_KeepsVariableVisible_ToLaterFields()
    {
        var format = Load(FieldEndianYaml);
        var root = new BinaryDecoder().Decode(new byte[] { 0x01, 0x02, 0xAA }, format);

        ((DecodedInteger)root.Children[0]).Value.Should().Be(0x0102, "big endian で読む");
        var body = root.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        body.StructType.Should().Be("one", "switch_on が tag を参照できる");
    }

    [Fact]
    public void FieldLevelEndianness_OnStructField_KeepsMemberAccess()
    {
        var format = Load("""
            name: T
            endianness: little
            root: main
            structs:
              main:
                - name: hdr
                  type: struct
                  struct: hdr
                  endianness: big
                - name: n
                  type: virtual
                  value: "{hdr.len}"
              hdr:
                - name: len
                  type: uint16
            """);
        var root = new BinaryDecoder().Decode(new byte[] { 0x00, 0x05 }, format);
        ((DecodedVirtual)root.Children[1]).Value.Should().Be(5L);
    }

    // ---------- 2. 巨大なサイズ式が負の int に化けて、スコープと位置が逆走していた ----------

    private const string SizedYaml = """
        name: T
        endianness: little
        root: main
        structs:
          main:
            - name: len
              type: uint32
            - name: body
              type: bytes
              size: "{len}"
            - name: tail
              type: uint8
        """;

    [Theory]
    [InlineData(0xFFFFFFFFu)] // int にキャストすると -1
    [InlineData(0x7FFFFFFFu)] // position + size が int で桁あふれ
    [InlineData(0x80000000u)]
    public void HugeSizeExpression_IsADecodeError_NotANegativeRange(uint len)
    {
        var format = Load(SizedYaml);
        var data = new byte[8];
        BitConverter.GetBytes(len).CopyTo(data, 0);

        var act = () => new BinaryDecoder().Decode(data, format);
        act.Should().Throw<DecodeException>();

        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        result.Errors.Should().NotBeEmpty();
        AssertNonNegative(result.Root, data.Length);
    }

    private static void AssertNonNegative(DecodedNode node, long dataLength)
    {
        node.Offset.Should().BeInRange(0, dataLength, node.Name);
        node.Size.Should().BeGreaterThanOrEqualTo(0, node.Name);
        (node.Offset + node.Size).Should().BeLessThanOrEqualTo(dataLength, node.Name);
        switch (node)
        {
            case DecodedStruct s:
                foreach (var c in s.Children) AssertNonNegative(c, dataLength);
                break;
            case DecodedArray a:
                foreach (var e in a.Elements) AssertNonNegative(e, dataLength);
                break;
        }
    }

    // ---------- 3. 壊れた count と入れ子のエラー要素で count 回まわり続けていた（msgpack の 1 バイト入力） ----------

    private const string RecursiveYaml = """
        name: MP
        endianness: big
        root: main
        structs:
          main:
            - name: value
              type: struct
              struct: value
          value:
            - name: format_byte
              type: uint8
            - name: data
              type: switch
              switch_on: "{format_byte}"
              cases:
                "0x81": map1
              default: nil
          map1:
            - name: entries
              type: struct
              struct: entry
              repeat_count: "{format_byte - 0x80}"
          entry:
            - name: key
              type: struct
              struct: value
            - name: val
              type: struct
              struct: value
          nil: []
        """;

    [Fact(Timeout = 10000)]
    public async Task RecursiveFormat_TruncatedInput_ContinueMode_Terminates_WithoutStackOverflow()
    {
        await Task.Yield();
        var format = Load(RecursiveYaml);
        // fixmap(1) だけで終わる → entry.key.format_byte が読めない。
        // 修正前: 失敗した format_byte が外側の 0x81 にフォールバックして無限再帰（スタックオーバーフロー）
        var result = new BinaryDecoder().DecodeWithRecovery(new byte[] { 0x81 }, format, ErrorMode.Continue);

        result.Errors.Should().NotBeEmpty();
        var value = result.Root.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var data = value.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var entries = data.Children[0].Should().BeOfType<DecodedArray>().Subject;
        entries.Elements.Should().HaveCount(1);
        var entry = entries.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var key = entry.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        key.Children[0].Should().BeOfType<DecodedError>("format_byte が読めない");
        key.Children[1].Should().BeOfType<DecodedError>("失敗した format_byte は外側の値にフォールバックしない");
    }

    [Fact(Timeout = 10000)]
    public async Task CountLoop_ZeroProgressErrorElements_AreTruncated_EvenWhenErrorIsNested()
    {
        await Task.Yield();
        var format = Load("""
            name: T
            endianness: big
            root: main
            structs:
              main:
                - name: count
                  type: uint32
                - name: items
                  type: struct
                  struct: item
                  repeat_count: "{count}"
              item:
                - name: inner
                  type: struct
                  struct: inner
              inner:
                - name: v
                  type: uint8
            """);
        // count = 2^31-1、要素は 1 つも読めない（入れ子の奥で失敗）
        var data = new byte[] { 0x7F, 0xFF, 0xFF, 0xFF };
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);

        var items = result.Root.Children[1].Should().BeOfType<DecodedArray>().Subject;
        items.Elements.Should().HaveCount(1, "位置が進まないエラー要素で打ち切る");
        items.Truncated.Should().BeTrue();
        items.TruncationReason.Should().Contain("no progress");
    }

    // ---------- 4. 入れ子の深さの上限（スタックオーバーフロー対策） ----------

    private const string DeepYaml = """
        name: Deep
        endianness: big
        root: main
        structs:
          main:
            - name: node
              type: struct
              struct: node
          node:
            - name: more
              type: uint8
            - name: child
              type: struct
              struct: node
              if: "{more == 1}"
        """;

    [Fact]
    public void NestingDeeperThanLimit_IsADecodeError_AndLimitIsConfigurable()
    {
        var format = Load(DeepYaml);
        const int levels = 100;
        var data = new byte[levels];
        Array.Fill(data, (byte)1);
        data[^1] = 0;

        var act = () => new BinaryDecoder().Decode(data, format);
        act.Should().Throw<DecodeException>().WithMessage($"*deeper than {BinaryDecoder.DefaultMaxDepth}*");

        // 上限を上げれば読める。深い再帰は 1 段あたり Debug で約 8 KB スタックを使うので、テストでは大きめのスタックで回す
        var deep = RunWithLargeStack(() => new BinaryDecoder().Decode(data, format, new DecodeOptions { MaxDepth = 200 }));
        var depth = 0;
        for (DecodedNode n = deep.Children[0]; n is DecodedStruct s && s.Children.Count > 1; n = s.Children[1])
            depth++;
        depth.Should().Be(levels - 1);

        // エラー継続モードでは深すぎた枝だけがエラーになり、全体は返る
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        result.Errors.Should().ContainSingle().Which.Message.Should().Contain("deeper than");
    }

    private static T RunWithLargeStack<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        }, 64 * 1024 * 1024);
        thread.Start();
        thread.Join();
        if (error is not null) throw error;
        return result;
    }

    // ---------- 5. 外側の同名変数へのフォールバック ----------

    [Fact]
    public void FailedField_ShadowsOuterVariableOfSameName_InContinueMode()
    {
        var format = Load("""
            name: T
            endianness: big
            root: main
            structs:
              main:
                - name: n
                  type: uint8
                - name: inner
                  type: struct
                  struct: inner
              inner:
                - name: n
                  type: uint8
                - name: body
                  type: bytes
                  size: "{n}"
            """);
        // 外側の n = 2 は読める。内側の n は EOF で失敗 → body は外側の n(=2) を使ってはいけない
        var result = new BinaryDecoder().DecodeWithRecovery(new byte[] { 0x02 }, format, ErrorMode.Continue);
        var inner = result.Root.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        inner.Children[0].Should().BeOfType<DecodedError>();
        var body = inner.Children[1].Should().BeOfType<DecodedError>().Subject;
        body.ErrorMessage.Should().Contain("'n'").And.Contain("not found");
    }

    // ---------- 6. seek で先頭より前へ戻る struct のサイズが負になっていた（ELF の body） ----------

    [Fact]
    public void StructThatSeeksBackwards_HasNonNegativeSize()
    {
        var format = Load("""
            name: T
            endianness: big
            root: main
            structs:
              main:
                - name: skip
                  type: bytes
                  size: "4"
                - name: body
                  type: struct
                  struct: body
              body:
                - name: back
                  type: uint8
                  seek: "0"
            """);
        var root = new BinaryDecoder().Decode(new byte[] { 1, 2, 3, 4, 5 }, format);
        var body = root.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        body.Offset.Should().Be(4);
        body.Size.Should().Be(0, "終了位置 (1) が開始位置 (4) より前なので 0 に丸める");
    }
}
