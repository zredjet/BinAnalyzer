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
        var decoded = Decode(MachoTestDataGenerator.CreateMinimalMacho64());

        decoded.Name.Should().Be("Mach-O");
        decoded.Children.Select(c => c.Name).Should().Equal("slice_offset", "magic", "body");
    }

    [Fact]
    public void MachoFormat_Header_DecodesCorrectly()
    {
        var decoded = Decode(MachoTestDataGenerator.CreateMinimalMacho64());

        var magic = (DecodedInteger)Child(decoded, "magic");
        magic.Value.Should().Be(unchecked((long)0xFEEDFACF));
        magic.EnumLabel.Should().Be("MH_MAGIC_64");
        var body = Child(decoded, "body");
        ((DecodedInteger)Child(body, "cputype")).EnumLabel.Should().Be("CPU_TYPE_ARM64");
        ((DecodedInteger)Child(body, "filetype")).EnumLabel.Should().Be("MH_EXECUTE");
        ((DecodedInteger)Child(body, "ncmds")).Value.Should().Be(1);
    }

    [Fact]
    public void MachoFormat_LoadCommand_DecodesCorrectly()
    {
        var body = Child(Decode(MachoTestDataGenerator.CreateMinimalMacho64()), "body");

        var lc = ((DecodedArray)Child(body, "load_commands")).Elements.Single();
        var cmd = (DecodedInteger)Child(lc, "cmd");
        cmd.Value.Should().Be(0x1B);
        cmd.EnumLabel.Should().Be("LC_UUID");
    }

    [Fact]
    public void MachoFormat_BuildVersion_ToolEntryDecodesCorrectly()
    {
        // 以前の定義は LC_BUILD_VERSION を 44（本当は LC_ENCRYPTION_INFO_64）としていた。正しくは 0x32
        var body = Child(Decode(MachoTestDataGenerator.CreateMacho64WithBuildVersion()), "body");

        var lc = ((DecodedArray)Child(body, "load_commands")).Elements.Single();
        ((DecodedInteger)Child(lc, "cmd")).EnumLabel.Should().Be("LC_BUILD_VERSION");
        var lcBody = Child(lc, "body");
        ((DecodedInteger)Child(lcBody, "ntools")).Value.Should().Be(1);
        var tool = ((DecodedArray)Child(lcBody, "tools")).Elements.Single();
        ((DecodedInteger)Child(tool, "tool")).EnumLabel.Should().Be("TOOL_LD");
        ((DecodedInteger)Child(tool, "version")).Value.Should().Be(0x003C0600);
    }

    [Fact]
    public void MachoFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(MachoTestDataGenerator.CreateMinimalMacho64()));

        output.Should().Contain("Mach-O");
        output.Should().Contain("CPU_TYPE_ARM64");
        output.Should().Contain("MH_EXECUTE");
        output.Should().Contain("LC_UUID");
    }

    [Fact]
    public void UniversalBinary_DecodesBothSlicesWithTheirEndianness()
    {
        var decoded = Decode(MachoTestDataGenerator.CreateUniversalBinary());

        ((DecodedInteger)Child(decoded, "magic")).EnumLabel.Should().Be("FAT_MAGIC");
        var archs = ((DecodedArray)Child(Child(decoded, "body"), "archs")).Elements;
        archs.Should().HaveCount(2);

        // スライス 1: 64 ビット リトルエンディアンの arm64
        var arm64 = Child(Child(archs[0], "image"), "body");
        ((DecodedInteger)Child(arm64, "cputype")).EnumLabel.Should().Be("CPU_TYPE_ARM64");
        var commands = ((DecodedArray)Child(arm64, "load_commands")).Elements;
        commands.Select(c => ((DecodedInteger)Child(c, "cmd")).EnumLabel).Should().Equal(
            "LC_SEGMENT_64", "LC_SEGMENT_64", "LC_SYMTAB", "LC_LOAD_DYLIB", "LC_BUILD_VERSION", "LC_CODE_SIGNATURE");
        var dylib = Child(commands[3], "body");
        ((DecodedString)Child(dylib, "name")).Value.Should().Be("/usr/lib/libSystem.B.dylib");
        ((DecodedVirtual)Child(dylib, "current_version_text")).Value.Should().Be("1356.0.0");
        ((DecodedVirtual)Child(Child(commands[4], "body"), "minos_text")).Value.Should().Be("14.0.0");

        var symbols = ((DecodedArray)Child(Child(arm64, "symbol_table"), "symbols")).Elements;
        ((DecodedString)Child(symbols[0], "name")).Value.Should().Be("_main");
        ((DecodedVirtual)Child(symbols[0], "symbol_type")).EnumLabel.Should().Be("N_SECT");
        ((DecodedString)Child(symbols[1], "name")).Value.Should().Be("_helper");
        ((DecodedVirtual)Child(symbols[1], "symbol_type")).EnumLabel.Should().Be("N_UNDF");

        var signature = Child(arm64, "code_signature");
        Child(signature, "magic").Validation!.Passed.Should().BeTrue();
        var blobs = ((DecodedArray)Child(signature, "blobs")).Elements;
        var codeDirectory = Child(blobs[0], "body");
        ((DecodedString)Child(codeDirectory, "identifier")).Value.Should().Be("com.example.gen");
        ((DecodedInteger)Child(codeDirectory, "hash_type")).EnumLabel.Should().Be("CS_HASHTYPE_SHA256");
        ((DecodedString)Child(Child(blobs[1], "body"), "xml")).Value.Should().Contain("get-task-allow");

        // スライス 2: 32 ビット ビッグエンディアンの PowerPC（シンボル表の位置はスライスの先頭から）
        var ppcImage = Child(archs[1], "image");
        ((DecodedInteger)Child(ppcImage, "magic")).EnumLabel.Should().Be("MH_CIGAM");
        var ppc = Child(ppcImage, "body");
        ((DecodedInteger)Child(ppc, "cputype")).EnumLabel.Should().Be("CPU_TYPE_POWERPC");
        var segment = Child(((DecodedArray)Child(ppc, "load_commands")).Elements[0], "body");
        ((DecodedString)Child(segment, "segname")).Value.Should().StartWith("__TEXT");
        var ppcSymbol = ((DecodedArray)Child(Child(ppc, "symbol_table"), "symbols")).Elements.Single();
        ((DecodedString)Child(ppcSymbol, "name")).Value.Should().Be("_start");
        ((DecodedInteger)Child(ppcSymbol, "n_value")).Value.Should().Be(0x1200);
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(MachoFormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
