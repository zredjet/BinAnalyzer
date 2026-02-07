using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class YamlFormatLoaderTests
{
    private readonly YamlFormatLoader _loader = new();

    [Fact]
    public void LoadFromString_MinimalFormat()
    {
        var yaml = """
            name: Test
            endianness: big
            root: main
            structs:
              main:
                - name: magic
                  type: uint32
            """;

        var format = _loader.LoadFromString(yaml);

        format.Name.Should().Be("Test");
        format.Endianness.Should().Be(Endianness.Big);
        format.RootStruct.Should().Be("main");
        format.Structs.Should().ContainKey("main");
        format.Structs["main"].Fields.Should().HaveCount(1);
        format.Structs["main"].Fields[0].Name.Should().Be("magic");
        format.Structs["main"].Fields[0].Type.Should().Be(FieldType.UInt32);
    }

    [Fact]
    public void LoadFromString_LittleEndian()
    {
        var yaml = """
            name: Test
            endianness: little
            root: main
            structs:
              main:
                - name: x
                  type: uint8
            """;

        var format = _loader.LoadFromString(yaml);
        format.Endianness.Should().Be(Endianness.Little);
    }

    [Fact]
    public void LoadFromString_WithEnums()
    {
        var yaml = """
            name: Test
            root: main
            enums:
              color:
                - value: 0
                  label: red
                  description: "The color red"
                - value: 1
                  label: green
            structs:
              main:
                - name: c
                  type: uint8
                  enum: color
            """;

        var format = _loader.LoadFromString(yaml);

        format.Enums.Should().ContainKey("color");
        var colorEnum = format.Enums["color"];
        colorEnum.Entries.Should().HaveCount(2);
        colorEnum.Entries[0].Label.Should().Be("red");
        colorEnum.Entries[0].Description.Should().Be("The color red");
        colorEnum.FindByValue(1)!.Label.Should().Be("green");

        format.Structs["main"].Fields[0].EnumRef.Should().Be("color");
    }

    [Fact]
    public void LoadFromString_WithFlags()
    {
        var yaml = """
            name: Test
            root: main
            flags:
              my_flags:
                bit_size: 8
                fields:
                  - name: active
                    bit: 0
                    set: "yes"
                    clear: "no"
            structs:
              main:
                - name: f
                  type: uint8
                  flags: my_flags
            """;

        var format = _loader.LoadFromString(yaml);

        format.Flags.Should().ContainKey("my_flags");
        var flags = format.Flags["my_flags"];
        flags.BitSize.Should().Be(8);
        flags.Fields.Should().HaveCount(1);
        flags.Fields[0].Name.Should().Be("active");
        flags.Fields[0].BitPosition.Should().Be(0);
        flags.Fields[0].SetMeaning.Should().Be("yes");
    }

    [Fact]
    public void LoadFromString_FieldWithSizeExpression()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: length
                  type: uint32
                - name: data
                  type: bytes
                  size: "{length}"
            """;

        var format = _loader.LoadFromString(yaml);

        var dataField = format.Structs["main"].Fields[1];
        dataField.Size.Should().BeNull();
        dataField.SizeExpression.Should().NotBeNull();
        dataField.SizeExpression!.OriginalText.Should().Be("{length}");
    }

    [Fact]
    public void LoadFromString_FieldWithSizeRemaining()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: rest
                  type: bytes
                  size: remaining
            """;

        var format = _loader.LoadFromString(yaml);
        format.Structs["main"].Fields[0].SizeRemaining.Should().BeTrue();
    }

    [Fact]
    public void LoadFromString_RepeatEof()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: items
                  type: struct
                  struct: item
                  repeat: eof
              item:
                - name: x
                  type: uint8
            """;

        var format = _loader.LoadFromString(yaml);
        format.Structs["main"].Fields[0].Repeat.Should().BeOfType<RepeatMode.UntilEof>();
    }

    [Fact]
    public void LoadFromString_RepeatCount()
    {
        var yaml = """
            name: Test
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
                - name: x
                  type: uint8
            """;

        var format = _loader.LoadFromString(yaml);
        format.Structs["main"].Fields[1].Repeat.Should().BeOfType<RepeatMode.Count>();
    }

    [Fact]
    public void LoadFromString_SwitchField()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: type
                  type: uint8
                - name: body
                  type: switch
                  switch_on: "{type}"
                  cases:
                    "1": type_a
                    "2": type_b
                  default: raw
              type_a:
                - name: a
                  type: uint32
              type_b:
                - name: b
                  type: uint16
              raw:
                - name: data
                  type: bytes
                  size: remaining
            """;

        var format = _loader.LoadFromString(yaml);

        var body = format.Structs["main"].Fields[1];
        body.Type.Should().Be(FieldType.Switch);
        body.SwitchOn.Should().NotBeNull();
        body.SwitchCases.Should().HaveCount(2);
        body.SwitchDefault.Should().Be("raw");
    }

    [Fact]
    public void LoadFromString_ExpectedBytes()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: sig
                  type: bytes
                  size: "4"
                  expected: [0x89, 0x50, 0x4E, 0x47]
            """;

        var format = _loader.LoadFromString(yaml);

        var sig = format.Structs["main"].Fields[0];
        sig.Expected.Should().NotBeNull();
        sig.Expected.Should().BeEquivalentTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
    }

    [Fact]
    public void LoadFromString_PngDefinition_LoadsSuccessfully()
    {
        var pngYaml = File.ReadAllText("../../../../../formats/png.bdef.yaml");
        var format = _loader.LoadFromString(pngYaml);

        format.Name.Should().Be("PNG");
        format.Endianness.Should().Be(Endianness.Big);
        format.RootStruct.Should().Be("png");
        format.Structs.Should().ContainKey("png");
        format.Structs.Should().ContainKey("chunk");
        format.Structs.Should().ContainKey("ihdr");
        format.Enums.Should().ContainKey("color_type");
        format.Flags.Should().ContainKey("chunk_type_flags");

        // Check PNG signature expected bytes
        var sig = format.Structs["png"].Fields[0];
        sig.Expected.Should().BeEquivalentTo(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // Check chunk structure
        var chunkFields = format.Structs["chunk"].Fields;
        chunkFields.Should().HaveCount(4);
        chunkFields[2].Type.Should().Be(FieldType.Switch);
        chunkFields[2].SwitchCases.Should().HaveCount(13);
    }

    [Fact]
    public void LoadFromString_InvalidRoot_Throws()
    {
        var yaml = """
            name: Test
            root: nonexistent
            structs:
              main:
                - name: x
                  type: uint8
            """;

        var act = () => _loader.LoadFromString(yaml);
        act.Should().Throw<InvalidOperationException>();
    }
}
