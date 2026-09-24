using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class WasmParsingTests
{
    private static readonly string WasmFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "wasm.bdef.yaml");

    [Fact]
    public void WasmFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(WasmFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void WasmFormat_DecodesMinimalWasm()
    {
        var decoded = Decode(WasmTestDataGenerator.CreateMinimalWasm());

        decoded.Name.Should().Be("WASM");
        decoded.Children.Select(c => c.Name).Should().Equal("magic", "version", "layer", "body");
        Child(Child(decoded, "body"), "sections").Should().BeOfType<DecodedArray>();
    }

    [Fact]
    public void WasmFormat_Magic_DecodesCorrectly()
    {
        ((DecodedBytes)Child(Decode(WasmTestDataGenerator.CreateMinimalWasm()), "magic")).ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void WasmFormat_Version_ValidatesCorrectly()
    {
        var decoded = Decode(WasmTestDataGenerator.CreateMinimalWasm());

        ((DecodedInteger)Child(decoded, "version")).Value.Should().Be(1);
        ((DecodedInteger)Child(decoded, "layer")).Value.Should().Be(0);
        Child(decoded, "layer").Validation!.Passed.Should().BeTrue();
    }

    [Fact]
    public void WasmFormat_Section_DecodesCorrectly()
    {
        var section = Sections(WasmTestDataGenerator.CreateMinimalWasm()).Single();

        var sectionId = (DecodedInteger)Child(section, "section_id");
        sectionId.Value.Should().Be(1);
        sectionId.EnumLabel.Should().Be("type");
    }

    [Fact]
    public void WasmFormat_SectionSize_DecodesAsULeb128()
    {
        var sectionSize = (DecodedInteger)Child(Sections(WasmTestDataGenerator.CreateMinimalWasm()).Single(), "section_size");
        sectionSize.Value.Should().Be(4);
        sectionSize.Size.Should().Be(1); // 1-byte LEB128
    }

    [Fact]
    public void WasmFormat_TypeSection_DecodesStructuredContent()
    {
        var sectionData = Child(Sections(WasmTestDataGenerator.CreateMinimalWasm()).Single(), "section_data");

        ((DecodedInteger)Child(sectionData, "count")).Value.Should().Be(1);
        var type = ((DecodedArray)Child(sectionData, "entries")).Elements.Single();
        var form = (DecodedInteger)Child(type, "form");
        form.Value.Should().Be(0x60);
        form.EnumLabel.Should().Be("func");
        var funcType = Child(type, "body");
        ((DecodedInteger)Child(funcType, "param_count")).Value.Should().Be(0);
        ((DecodedInteger)Child(funcType, "result_count")).Value.Should().Be(0);
    }

    [Fact]
    public void WasmFormat_ExportSection_DecodesCorrectly()
    {
        var exportSection = Sections(WasmTestDataGenerator.CreateWasmWithExportSection())
            .First(s => ((DecodedInteger)Child(s, "section_id")).Value == 7);
        var sectionData = Child(exportSection, "section_data");

        ((DecodedInteger)Child(sectionData, "count")).Value.Should().Be(1);
        var entry = ((DecodedArray)Child(sectionData, "entries")).Elements.Single();
        ((DecodedString)Child(entry, "name")).Value.Should().Be("main");
        ((DecodedInteger)Child(entry, "kind")).EnumLabel.Should().Be("func");
        ((DecodedInteger)Child(entry, "index")).Value.Should().Be(0);
    }

    [Fact]
    public void WasmFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(WasmTestDataGenerator.CreateMinimalWasm()));

        output.Should().Contain("WASM");
        output.Should().Contain("magic");
        output.Should().Contain("version");
        output.Should().Contain("sections");
        output.Should().Contain("type");
    }

    [Fact]
    public void Wasm3Module_DecodesGcTypesImportsConstantExpressionsAndNames()
    {
        var sections = Sections(WasmTestDataGenerator.CreateWasm3Module());
        DecodedNode Data(string id) => Child(sections.First(s => ((DecodedInteger)Child(s, "section_id")).EnumLabel == id), "section_data");

        var types = ((DecodedArray)Child(Data("type"), "entries")).Elements;
        types.Select(t => ((DecodedInteger)Child(t, "form")).EnumLabel).Should().Equal("func", "func", "rec", "array");
        var func = Child(types[0], "body");
        ((DecodedArray)Child(func, "params")).Elements.Select(p => ((DecodedInteger)Child(p, "code")).EnumLabel).Should().Equal("i32", "i32");
        // rec { sub struct { mut i32, (ref null 2) } }
        var sub = ((DecodedArray)Child(Child(types[2], "body"), "types")).Elements.Single();
        ((DecodedInteger)Child(sub, "form")).EnumLabel.Should().Be("sub");
        var structType = Child(Child(sub, "body"), "comp_type");
        var fields = ((DecodedArray)Child(structType, "fields")).Elements;
        ((DecodedInteger)Child(fields[0], "mutable")).Value.Should().Be(1);
        var refField = Child(fields[1], "storage_type");
        ((DecodedInteger)Child(refField, "code")).EnumLabel.Should().Be("ref_null");
        ((DecodedInteger)Child(refField, "heap_type")).Value.Should().Be(2);
        ((DecodedInteger)Child(Child(Child(Child(types[3], "body"), "element"), "storage_type"), "code")).EnumLabel.Should().Be("i8");

        var imports = ((DecodedArray)Child(Data("import"), "entries")).Elements;
        imports.Select(i => ((DecodedInteger)Child(i, "kind")).EnumLabel).Should().Equal("func", "memory", "global", "tag");
        // メモリの最大値（limits の flags の bit 0）。以前の定義は最大値を読んでいなかった
        var memory = Child(imports[1], "desc");
        ((DecodedInteger)Child(memory, "min")).Value.Should().Be(1);
        ((DecodedInteger)Child(memory, "max")).Value.Should().Be(2);

        // 拡張定数式: i32.const 40、i32.const 2、i32.add、end
        var global = ((DecodedArray)Child(Data("global"), "globals")).Elements.Single();
        var instructions = ((DecodedArray)Child(Child(global, "init"), "instructions")).Elements;
        instructions.Select(i => ((DecodedInteger)Child(i, "opcode")).EnumLabel).Should().Equal("i32.const", "i32.const", "i32.add", "end");
        ((DecodedInteger)Child(Child(instructions[0], "immediate"), "value")).Value.Should().Be(40);

        var element = ((DecodedArray)Child(Data("element"), "segments")).Elements.Single();
        ((DecodedArray)Child(element, "function_indices")).Elements.Cast<DecodedInteger>().Select(i => i.Value).Should().Equal(1L);

        var body = Child(((DecodedArray)Child(Data("code"), "bodies")).Elements.Single(), "body");
        var local = ((DecodedArray)Child(body, "locals")).Elements.Single();
        ((DecodedInteger)Child(Child(local, "val_type"), "code")).EnumLabel.Should().Be("i64");
        ((DecodedBytes)Child(body, "instructions")).RawBytes.Length.Should().Be(6);

        var segments = ((DecodedArray)Child(Data("data"), "segments")).Elements;
        ((DecodedBytes)Child(segments[0], "data")).RawBytes.ToArray().Should().Equal("hi"u8.ToArray());
        ((DecodedStruct)segments[1]).Children.Should().NotContain(c => c.Name == "offset", "パッシブのセグメントはオフセットを持たない");

        var customs = sections.Where(s => ((DecodedInteger)Child(s, "section_id")).Value == 0).Select(s => Child(s, "section_data")).ToList();
        var subsections = ((DecodedArray)Child(Child(customs[0], "content"), "subsections")).Elements;
        ((DecodedString)Child(Child(subsections[0], "content"), "value")).Value.Should().Be("gen");
        var functionName = ((DecodedArray)Child(Child(subsections[1], "content"), "entries")).Elements.Single();
        ((DecodedString)Child(Child(functionName, "name"), "value")).Value.Should().Be("add");
        var producer = ((DecodedArray)Child(Child(customs[1], "content"), "fields")).Elements.Single();
        ((DecodedString)Child(Child(producer, "field_name"), "value")).Value.Should().Be("language");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(WasmFormatPath));

    private static IReadOnlyList<DecodedNode> Sections(byte[] data) =>
        ((DecodedArray)Child(Child(Decode(data), "body"), "sections")).Elements;

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
