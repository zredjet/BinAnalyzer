using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ElfParsingTests
{
    private static readonly string ElfFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "elf.bdef.yaml");

    [Fact]
    public void ElfFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(ElfFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ElfFormat_DecodesMinimalElf64()
    {
        var elfData = ElfTestDataGenerator.CreateMinimalElf64();
        var format = new YamlFormatLoader().Load(ElfFormatPath);
        var decoded = new BinaryDecoder().Decode(elfData, format);

        decoded.Name.Should().Be("ELF");
        decoded.Children.Should().HaveCount(3);
        decoded.Children[0].Name.Should().Be("e_ident");
        decoded.Children[1].Name.Should().Be("header");
        decoded.Children[2].Name.Should().Be("program_headers");
    }

    [Fact]
    public void ElfFormat_Ident_DecodesCorrectly()
    {
        var elfData = ElfTestDataGenerator.CreateMinimalElf64();
        var format = new YamlFormatLoader().Load(ElfFormatPath);
        var decoded = new BinaryDecoder().Decode(elfData, format);

        var ident = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;

        var magic = ident.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();

        var eiClass = ident.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        eiClass.Value.Should().Be(2);
        eiClass.EnumLabel.Should().Be("ELFCLASS64");

        var eiData = ident.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        eiData.Value.Should().Be(1);
        eiData.EnumLabel.Should().Be("ELFDATA2LSB");
    }

    [Fact]
    public void ElfFormat_Header_DecodesCorrectly()
    {
        var elfData = ElfTestDataGenerator.CreateMinimalElf64();
        var format = new YamlFormatLoader().Load(ElfFormatPath);
        var decoded = new BinaryDecoder().Decode(elfData, format);

        var header = decoded.Children[1].Should().BeOfType<DecodedStruct>().Subject;

        var eType = header.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        eType.Value.Should().Be(2);
        eType.EnumLabel.Should().Be("ET_EXEC");

        var eMachine = header.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        eMachine.Value.Should().Be(62);
        eMachine.EnumLabel.Should().Be("EM_X86_64");

        var ePhnum = header.Children[9].Should().BeOfType<DecodedInteger>().Subject;
        ePhnum.Value.Should().Be(1);
    }

    [Fact]
    public void ElfFormat_ProgramHeaders_DecodesCorrectly()
    {
        var elfData = ElfTestDataGenerator.CreateMinimalElf64();
        var format = new YamlFormatLoader().Load(ElfFormatPath);
        var decoded = new BinaryDecoder().Decode(elfData, format);

        var phdrArray = decoded.Children[2].Should().BeOfType<DecodedArray>().Subject;
        phdrArray.Elements.Should().HaveCount(1);

        var phdr = phdrArray.Elements[0].Should().BeOfType<DecodedStruct>().Subject;

        var pType = phdr.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        pType.Value.Should().Be(1);
        pType.EnumLabel.Should().Be("PT_LOAD");

        var pFlags = phdr.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        pFlags.Value.Should().Be(5);
    }

    [Fact]
    public void ElfFormat_TreeOutput_ContainsExpectedElements()
    {
        var elfData = ElfTestDataGenerator.CreateMinimalElf64();
        var format = new YamlFormatLoader().Load(ElfFormatPath);
        var decoded = new BinaryDecoder().Decode(elfData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("ELF");
        output.Should().Contain("e_ident");
        output.Should().Contain("ELFCLASS64");
        output.Should().Contain("ELFDATA2LSB");
        output.Should().Contain("ET_EXEC");
        output.Should().Contain("EM_X86_64");
        output.Should().Contain("PT_LOAD");
        output.Should().Contain("program_headers");
    }
}
