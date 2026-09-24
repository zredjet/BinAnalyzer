using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>
/// REQ-190: size を指定した繰り返し（配列全体のバウンダリスコープ）でも、要素の値が
/// size の無い繰り返しと同じく外側のスコープに残ること。_index / _prev は繰り返しの中だけのもの
/// </summary>
public class SizedRepeatScopeTests
{
    private readonly BinaryDecoder _decoder = new();

    // ZIP の Zip64 と同じ形: ヘッダの 0xFF を拡張フィールドの値で置き換える。
    // 拡張フィールドは ID・長さ・switch（size 付き）で、Zip64 の値は switch の中にある
    private const string ExtraFieldsYaml = """
        name: t
        endianness: little
        root: main
        structs:
          main:
            - name: size32
              type: uint8
            - name: extra_length
              type: uint8
            - name: extra
              type: struct
              struct: extra_field
        {0}
            - name: actual_size
              type: virtual
              value: "{{size32 == 0xFF ? zip64_size : size32}}"
            - name: trailer
              type: uint8
          extra_field:
            - name: id
              type: uint8
            - name: len
              type: uint8
            - name: data
              type: switch
              switch_on: "{{id}}"
              size: "{{len}}"
              cases:
                "1": zip64
              default: raw
          zip64:
            - name: zip64_size
              type: uint16
          raw:
            - name: data
              type: bytes
              size: remaining
        """;

    // size32=0xFF, extra_length=7: [id=2 len=1 0xAA] [id=1 len=2 0x1234], trailer=0xEE
    private static readonly byte[] ExtraFieldsData =
        [0xFF, 0x07, 0x02, 0x01, 0xAA, 0x01, 0x02, 0x34, 0x12, 0xEE];

    public static TheoryData<string> SizedRepeats => new()
    {
        "      size: \"{extra_length}\"\n      repeat: eof",
        "      size: \"{extra_length}\"\n      repeat_count: \"2\"",
        "      size: \"{extra_length}\"\n      repeat_while: \"{remaining > 0}\"",
        "      size: \"{extra_length}\"\n      repeat_until: \"{id == 1}\"",
    };

    [Theory]
    [MemberData(nameof(SizedRepeats))]
    public void SizedRepeat_ElementValues_ReachTheEnclosingStruct(string repeat)
    {
        var result = Decode(ExtraFieldsYaml, repeat, ExtraFieldsData);

        Child<DecodedVirtual>(result, "actual_size").Value.Should().Be(0x1234L);
        Child<DecodedInteger>(result, "trailer").Value.Should().Be(0xEE);
    }

    [Fact]
    public void SizedRepeat_GivesTheSameValuesAsTheUnsizedRepeat()
    {
        var sized = Decode(ExtraFieldsYaml, "      size: \"{extra_length}\"\n      repeat_count: \"2\"", ExtraFieldsData);
        var unsized = Decode(ExtraFieldsYaml, "      repeat_count: \"2\"", ExtraFieldsData);

        Child<DecodedVirtual>(sized, "actual_size").Value
            .Should().Be(Child<DecodedVirtual>(unsized, "actual_size").Value);
    }

    [Fact]
    public void SameNameInTheEnclosingStruct_IsOverwrittenByTheLastElement_AsWithTheUnsizedRepeat()
    {
        // 外側の id（9）は、size の無い繰り返しと同じく最後の要素の id（1）で上書きされる
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: id
                  type: uint8
                - name: items
                  type: struct
                  struct: item
            {0}
                - name: id_after
                  type: virtual
                  value: "{{id}}"
              item:
                - name: id
                  type: uint8
            """;
        byte[] data = [9, 3, 1];

        var sized = Decode(yaml, "      size: \"2\"\n      repeat: eof", data);
        var unsized = Decode(yaml, "      repeat_count: \"2\"", data);

        Child<DecodedVirtual>(sized, "id_after").Value.Should().Be(1L);
        Child<DecodedVirtual>(unsized, "id_after").Value.Should().Be(1L);
    }

    [Fact]
    public void OuterRepeat_IndexAndPrev_AreNotOverwrittenByAnInnerSizedRepeat()
    {
        // record の中の items（size 付きの繰り返し）の後でも、_index / _prev は records のもの
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: records
                  type: struct
                  struct: record
                  repeat_count: "2"
              record:
                - name: n
                  type: uint8
                - name: items
                  type: struct
                  struct: item
                  size: "{n}"
                  repeat: eof
                - name: record_index
                  type: virtual
                  value: "{_index}"
                - name: prev_n
                  type: virtual
                  value: "{_prev.n}"
                  if: "{_index > 0}"
              item:
                - name: v
                  type: uint8
            """;
        // record 0: n=2 [0xA0 0xA1]、record 1: n=3 [0xB0 0xB1 0xB2]
        byte[] data = [2, 0xA0, 0xA1, 3, 0xB0, 0xB1, 0xB2];

        var result = _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));

        var records = Child<DecodedArray>(result, "records").Elements.Cast<DecodedStruct>().ToList();
        Child<DecodedVirtual>(records[0], "record_index").Value.Should().Be(0L);
        Child<DecodedVirtual>(records[1], "record_index").Value.Should().Be(1L);
        Child<DecodedVirtual>(records[1], "prev_n").Value.Should().Be(2L);
    }

    [Fact]
    public void NestedSizedRepeats_CarryValuesOutwardLevelByLevel()
    {
        // 要素の配列の中身は昇格しない（PromoteDecodedValues は配列を走査しない）ので、
        // 内側の繰り返しの値は、外側の繰り返しのスコープを経て main に届く
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: groups
                  type: struct
                  struct: group
                  size: "5"
                  repeat: eof
                - name: last_value
                  type: virtual
                  value: "{value}"
              group:
                - name: group_length
                  type: uint8
                - name: entries
                  type: struct
                  struct: entry
                  size: "{group_length}"
                  repeat: eof
              entry:
                - name: value
                  type: uint8
            """;
        // group 0: [0x11]、group 1: [0x21 0x22]
        byte[] data = [1, 0x11, 2, 0x21, 0x22];

        var result = _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));

        Child<DecodedVirtual>(result, "last_value").Value.Should().Be(0x22L);
    }

    // 値が届くのは最も近い変数のスコープ（ここでは size 付きの wrapper）まで。
    // wrapper の外からは、従来どおりメンバーアクセスで引く
    private const string WrapperYaml = """
        name: t
        endianness: little
        root: main
        structs:
          wrapper:
            - name: items
              type: struct
              struct: entry
              size: "3"
              repeat: eof
            - name: last
              type: virtual
              value: "{value}"
          entry:
            - name: value
              type: uint8
          main:
            - name: wrapper
              type: struct
              struct: wrapper
              size: "3"
            - name: via_member
              type: virtual
              value: "{wrapper.last}"

        """;

    [Fact]
    public void CarriedValues_ReachTheEnclosingSizedStruct_AndAreReadFromOutsideByMemberAccess()
    {
        var result = _decoder.Decode(new byte[] { 1, 2, 3 }, new YamlFormatLoader().LoadFromString(WrapperYaml));

        var wrapper = Child<DecodedStruct>(result, "wrapper");
        Child<DecodedVirtual>(wrapper, "last").Value.Should().Be(3L);
        Child<DecodedVirtual>(result, "via_member").Value.Should().Be(3L);
    }

    [Fact]
    public void CarriedValues_DoNotLeakOutOfTheEnclosingSizedStruct()
    {
        const string leaked = """
                - name: leaked
                  type: virtual
                  value: "{value}"
            """;

        var act = () => _decoder.Decode(new byte[] { 1, 2, 3 }, new YamlFormatLoader().LoadFromString(WrapperYaml + leaked));

        act.Should().Throw<DecodeException>().Where(e => e.FieldPath.EndsWith("leaked"));
    }

    [Fact]
    public void SizedScalarRepeat_NameStillRefersToTheWholeArray()
    {
        // 要素（整数）の名前も外に出るが、繰り返しの後に配列そのものが同じ名前で束縛される
        const string yaml = """
            name: t
            endianness: little
            root: main
            structs:
              main:
                - name: bytes
                  type: uint8
                  size: "3"
                  repeat: eof
                - name: second
                  type: virtual
                  value: "{bytes[1]}"
            """;
        byte[] data = [0x0A, 0x0B, 0x0C];

        var result = _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));

        Child<DecodedVirtual>(result, "second").Value.Should().Be(0x0BL);
    }

    private DecodedStruct Decode(string template, string repeat, byte[] data)
    {
        var yaml = string.Format(template, repeat);
        return _decoder.Decode(data, new YamlFormatLoader().LoadFromString(yaml));
    }

    private static T Child<T>(DecodedStruct parent, string name) where T : DecodedNode =>
        parent.Children.Single(c => c.Name == name).Should().BeOfType<T>().Subject;
}
