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
        var data = WasmTestDataGenerator.CreateMinimalWasm();
        var format = new YamlFormatLoader().Load(WasmFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("WASM");
        decoded.Children.Should().HaveCount(3);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("version");
        decoded.Children[2].Name.Should().Be("sections");
    }

    [Fact]
    public void WasmFormat_Magic_DecodesCorrectly()
    {
        var data = WasmTestDataGenerator.CreateMinimalWasm();
        var format = new YamlFormatLoader().Load(WasmFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void WasmFormat_Version_ValidatesCorrectly()
    {
        var data = WasmTestDataGenerator.CreateMinimalWasm();
        var format = new YamlFormatLoader().Load(WasmFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var version = decoded.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        version.Value.Should().Be(1);
    }

    [Fact]
    public void WasmFormat_Section_DecodesCorrectly()
    {
        var data = WasmTestDataGenerator.CreateMinimalWasm();
        var format = new YamlFormatLoader().Load(WasmFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var sections = decoded.Children[2].Should().BeOfType<DecodedArray>().Subject;
        sections.Elements.Should().HaveCount(1);

        var section = sections.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var sectionId = section.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        sectionId.Name.Should().Be("section_id");
        sectionId.Value.Should().Be(1);
        sectionId.EnumLabel.Should().Be("type");
    }

    [Fact]
    public void WasmFormat_SectionSize_DecodesAsULeb128()
    {
        var data = WasmTestDataGenerator.CreateMinimalWasm();
        var format = new YamlFormatLoader().Load(WasmFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var sections = decoded.Children[2].Should().BeOfType<DecodedArray>().Subject;
        var section = sections.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var sectionSize = section.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        sectionSize.Name.Should().Be("section_size");
        sectionSize.Value.Should().Be(4);
        sectionSize.Size.Should().Be(1); // 1-byte LEB128
    }

    [Fact]
    public void WasmFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = WasmTestDataGenerator.CreateMinimalWasm();
        var format = new YamlFormatLoader().Load(WasmFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("WASM");
        output.Should().Contain("magic");
        output.Should().Contain("version");
        output.Should().Contain("sections");
        output.Should().Contain("type");
    }
}
