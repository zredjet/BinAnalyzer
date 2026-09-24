using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-191: size 付きのスコープの中から、その範囲の外へ seek したフィールドは、
/// 行き先を含む外側の境界（無ければファイル全体）の中で読む。範囲の中への seek は従来どおり
/// </summary>
public class SeekBoundaryTests
{
    private readonly BinaryDecoder _decoder = new();

    // ELF のシンボル表と同じ形: size 付きの switch（セクションの中身）の中から、別の場所の文字列表の名前を seek で引く。
    // データ: [0] name_offset=6 [1] count=2 [2..5] section（2 個の名前の位置: 0, 4） [6..] 文字列表 "ab\0\0cd\0"
    private const string SymbolTableYaml = """
        name: t
        endianness: little
        root: main
        structs:
          main:
            - name: strtab
              type: uint8
            - name: count
              type: uint8
            - name: section
              type: switch
              switch_on: "{1}"
              size: "{count * 2}"
              cases:
                "1": symbols
          symbols:
            - name: entries
              type: struct
              struct: symbol
              repeat_count: "{count}"
          symbol:
            - name: st_name
              type: uint8
            - name: st_info
              type: uint8
            - name: name
              type: asciiz
              seek: "{strtab + st_name}"
              seek_restore: true
        """;

    private static readonly byte[] SymbolTableData =
        [6, 2, 0, 0x11, 4, 0x22, (byte)'a', (byte)'b', 0, 0, (byte)'c', (byte)'d', 0];

    [Fact]
    public void SeekOutsideSizedSwitch_ReadsTheTargetAndReturnsToTheScope()
    {
        var result = Decode(SymbolTableYaml, SymbolTableData);

        var entries = Child<DecodedArray>(Child<DecodedStruct>(result, "section"), "entries").Elements.Cast<DecodedStruct>().ToList();
        entries.Select(e => Child<DecodedString>(e, "name").Value).Should().Equal("ab", "cd");
        // seek_restore で元の位置に戻り、size 付きのスコープの中の続き（st_info）を正しく読む
        entries.Select(e => Child<DecodedInteger>(e, "st_info").Value).Should().Equal(0x11L, 0x22L);
    }

    [Fact]
    public void PerElementSeekOutsideSizedScope_ReadsEachTarget()
    {
        // 要素ごとの seek（_index を使う）でも、各要素を行き先を含む境界の中で読む
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: table
                  type: struct
                  struct: table
                  size: "2"
                - name: tail
                  type: uint8
              table:
                - name: offsets
                  type: uint8
                  repeat_count: "2"
                - name: values
                  type: uint16
                  repeat_count: "2"
                  seek: "{offsets[_index]}"
                  seek_restore: true
            """;
        byte[] data = [3, 5, 0x99, 0x34, 0x12, 0x78, 0x56];

        var result = Decode(yaml, data);

        var values = Child<DecodedArray>(Child<DecodedStruct>(result, "table"), "values").Elements.Cast<DecodedInteger>();
        values.Select(v => v.Value).Should().Equal(0x1234L, 0x5678L);
        Child<DecodedInteger>(result, "tail").Value.Should().Be(0x99);
    }

    [Fact]
    public void SeekOutside_RemainingRefersToTheWidenedBoundary()
    {
        // 行き先を含む境界がルートなら、seek したフィールドの size: remaining はファイルの末尾まで
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: inner
                  type: struct
                  struct: inner
                  size: "2"
              inner:
                - name: pointer
                  type: uint8
                - name: rest
                  type: bytes
                  size: remaining
                  seek: "{pointer}"
                  seek_restore: true
                - name: after
                  type: uint8
            """;
        byte[] data = [3, 0x42, 0xFF, 0xA0, 0xA1, 0xA2];

        var inner = Child<DecodedStruct>(Decode(yaml, data), "inner");

        Child<DecodedBytes>(inner, "rest").RawBytes.ToArray().Should().Equal(0xA0, 0xA1, 0xA2);
        Child<DecodedInteger>(inner, "after").Value.Should().Be(0x42);
    }

    [Fact]
    public void SeekInsideTheCurrentScope_KeepsTheScopeBoundary()
    {
        // 行き先が今の境界の中なら従来どおり: remaining は size 付きのスコープの残り
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: inner
                  type: struct
                  struct: inner
                  size: "4"
                - name: outside
                  type: bytes
                  size: remaining
              inner:
                - name: pointer
                  type: uint8
                - name: rest
                  type: bytes
                  size: remaining
                  seek: "{pointer}"
                  seek_restore: true
            """;
        byte[] data = [2, 0, 0xB0, 0xB1, 0xC0, 0xC1];

        var result = Decode(yaml, data);

        Child<DecodedBytes>(Child<DecodedStruct>(result, "inner"), "rest").RawBytes.ToArray().Should().Equal(0xB0, 0xB1);
        Child<DecodedBytes>(result, "outside").RawBytes.ToArray().Should().Equal(0xC0, 0xC1);
    }

    [Fact]
    public void SeekOutsideTheInnerScope_WidensOnlyToTheEnclosingScopeThatContainsTheTarget()
    {
        // Mach-O のユニバーサルバイナリのスライスと同じ形: 外側の size 付きのスコープ（スライス）の中で、
        // 内側の size 付きのスコープ（ロードコマンド）の外へ seek すると、remaining はスライスの残りまで（ファイルの末尾ではない）
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: slice
                  type: struct
                  struct: slice
                  size: "5"
                - name: trailer
                  type: bytes
                  size: remaining
              slice:
                - name: command
                  type: struct
                  struct: command
                  size: "2"
                - name: payload
                  type: bytes
                  size: remaining
              command:
                - name: pointer
                  type: uint8
                - name: data
                  type: bytes
                  size: remaining
                  seek: "{pointer}"
                  seek_restore: true
            """;
        byte[] data = [3, 0, 0xD0, 0xD1, 0xD2, 0xEE, 0xEF];

        var slice = Child<DecodedStruct>(Decode(yaml, data), "slice");

        Child<DecodedBytes>(Child<DecodedStruct>(slice, "command"), "data").RawBytes.ToArray().Should().Equal(0xD1, 0xD2);
        Child<DecodedBytes>(slice, "payload").RawBytes.ToArray().Should().Equal(0xD0, 0xD1, 0xD2);
    }

    [Fact]
    public void SeekOutside_KeepsTheEndiannessAndBindsTheValueAsUsual()
    {
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: inner
                  type: struct
                  struct: inner
                  size: "1"
                - name: copied
                  type: virtual
                  value: "{inner.far}"
              inner:
                endianness: big
                fields:
                  - name: pointer
                    type: uint8
                  - name: far
                    type: uint16
                    seek: "{pointer}"
                    seek_restore: true
                  - name: doubled
                    type: virtual
                    value: "{far * 2}"
            """;
        byte[] data = [1, 0x01, 0x02];

        var result = Decode(yaml, data);

        var inner = Child<DecodedStruct>(result, "inner");
        Child<DecodedInteger>(inner, "far").Value.Should().Be(0x0102);
        Child<DecodedVirtual>(inner, "doubled").Value.Should().Be(0x0204L);
        Child<DecodedVirtual>(result, "copied").Value.Should().Be(0x0102L);
    }

    [Fact]
    public void SeekOutsideWithoutRestore_LeavesThePositionOutsideTheScope()
    {
        // seek_restore が無いと、位置は seek したフィールドの後ろのまま。size 付きのスコープの続きは従来どおり読めない
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: inner
                  type: struct
                  struct: inner
                  size: "2"
              inner:
                - name: pointer
                  type: uint8
                - name: far
                  type: uint8
                  seek: "{pointer}"
                - name: next
                  type: uint8
            """;
        byte[] data = [2, 0, 0x7F, 0x80];

        var act = () => Decode(yaml, data);

        act.Should().Throw<DecodeException>().WithMessage("*remaining in scope*");
    }

    [Fact]
    public void FailureInsideTheSeekedField_RestoresTheScopeBoundary()
    {
        // エラー継続モード: seek した先で読み切れなくても、広げたスコープは戻り、元の境界で続きを読む
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: inner
                  type: struct
                  struct: inner
                  size: "3"
                - name: tail
                  type: uint8
              inner:
                - name: pointer
                  type: uint8
                - name: far
                  type: uint32
                  seek: "{pointer}"
                  seek_restore: true
                - name: rest
                  type: bytes
                  size: remaining
            """;
        byte[] data = [3, 0xAA, 0xBB, 0xCC];

        var result = _decoder.DecodeWithRecovery(data, new YamlFormatLoader().LoadFromString(yaml), ErrorMode.Continue);

        result.Errors.Should().ContainSingle(e => e.FieldPath.EndsWith("far"));
        var inner = Child<DecodedStruct>(result.Root, "inner");
        Child<DecodedBytes>(inner, "rest").RawBytes.ToArray().Should().Equal(0xAA, 0xBB);
        Child<DecodedInteger>(result.Root, "tail").Value.Should().Be(0xCC);
    }

    [Fact]
    public void DecodeContext_PushSeekBoundary_WidensOnlyForTargetsOutsideTheCurrentBoundary()
    {
        var context = new DecodeContext(new byte[10], Endianness.Little);
        context.Seek(2);
        context.PushScope(4);                  // [2, 6]
        var depth = context.ScopeDepth;

        context.PushSeekBoundary(2).Should().BeFalse();
        context.PushSeekBoundary(5).Should().BeFalse();
        context.PushSeekBoundary(10).Should().BeFalse("ファイルの終わりはどの境界も含まないので広げない");
        context.ScopeDepth.Should().Be(depth);

        context.Seek(6);
        context.PushSeekBoundary(6).Should().BeTrue("境界の終わりちょうどのバイトは境界の外");
        context.Remaining.Should().Be(4, "ルート（0..10）まで広がる");
        context.ScopeDepth.Should().Be(depth + 1);
        context.PushSeekBoundary(1).Should().BeFalse("広げたスコープが今の境界になる");

        context.PopScopesTo(depth);
        context.Position.Should().Be(6, "広げたスコープの Pop は位置を動かさない");
        context.Seek(3);
        context.Remaining.Should().Be(3, "元の境界 [2, 6) に戻る");
    }

    private DecodedStruct Decode(string yaml, byte[] data) =>
        _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));

    private static T Child<T>(DecodedStruct parent, string name) where T : DecodedNode =>
        parent.Children.Single(c => c.Name == name).Should().BeOfType<T>().Subject;
}
