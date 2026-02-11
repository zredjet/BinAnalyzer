using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class MachoParsingTests
{
    private static readonly string MachoFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "macho.bdef.yaml");

    [Fact]
    public void MachoFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(MachoFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MachoFormat_DecodesMinimalMacho64()
    {
        var data = MachoTestDataGenerator.CreateMinimalMacho64();
        var format = new YamlFormatLoader().Load(MachoFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("Mach-O");
        decoded.Children.Should().HaveCount(2);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("body");
    }

    [Fact]
    public void MachoFormat_Header_DecodesCorrectly()
    {
        var data = MachoTestDataGenerator.CreateMinimalMacho64();
        var format = new YamlFormatLoader().Load(MachoFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        magic.Value.Should().Be(unchecked((long)0xFEEDFACF));

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        var cputype = body.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        cputype.EnumLabel.Should().Be("CPU_TYPE_ARM64");

        var filetype = body.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        filetype.Value.Should().Be(2);
        filetype.EnumLabel.Should().Be("MH_EXECUTE");

        var ncmds = body.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        ncmds.Value.Should().Be(1);
    }

    [Fact]
    public void MachoFormat_LoadCommand_DecodesCorrectly()
    {
        var data = MachoTestDataGenerator.CreateMinimalMacho64();
        var format = new YamlFormatLoader().Load(MachoFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var loadCommands = body.Children.Last().Should().BeOfType<DecodedArray>().Subject;
        loadCommands.Elements.Should().HaveCount(1);

        var lc = loadCommands.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var cmd = lc.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        cmd.Value.Should().Be(27);
        cmd.EnumLabel.Should().Be("LC_UUID");
    }

    [Fact]
    public void MachoFormat_BuildVersion_ToolEntryDecodesCorrectly()
    {
        var data = MachoTestDataGenerator.CreateMacho64WithBuildVersion();
        var format = new YamlFormatLoader().Load(MachoFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var body = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;
        var loadCommands = body.Children.Last().Should().BeOfType<DecodedArray>().Subject;
        var lc = loadCommands.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        // cmd=44 (LC_BUILD_VERSION), cmdsize=32, body (switch → build_version_body)
        var cmd = lc.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        cmd.Value.Should().Be(44);
        cmd.EnumLabel.Should().Be("LC_BUILD_VERSION");

        var lcBody = lc.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        // build_version_body: platform, minos, sdk, ntools, tools
        var ntools = lcBody.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        ntools.Value.Should().Be(1);

        var tools = lcBody.Children[4].Should().BeOfType<DecodedArray>().Subject;
        tools.Elements.Should().HaveCount(1);

        var toolEntry = tools.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var tool = toolEntry.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        tool.Name.Should().Be("tool");
        tool.Value.Should().Be(3); // ld

        var version = toolEntry.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        version.Name.Should().Be("version");
        version.Value.Should().Be(0x003C0600);
    }

    [Fact]
    public void MachoFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = MachoTestDataGenerator.CreateMinimalMacho64();
        var format = new YamlFormatLoader().Load(MachoFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("Mach-O");
        output.Should().Contain("CPU_TYPE_ARM64");
        output.Should().Contain("MH_EXECUTE");
        output.Should().Contain("LC_UUID");
    }
}
