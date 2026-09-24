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
        var decoded = Decode(ElfTestDataGenerator.CreateMinimalElf64());

        decoded.Name.Should().Be("ELF");
        decoded.Children.Select(c => c.Name).Should().Equal("e_ident", "body");
        var body = Child(decoded, "body");
        Child(body, "header").Should().BeOfType<DecodedStruct>();
        ((DecodedArray)Child(body, "program_headers")).Elements.Should().HaveCount(1);
        // e_shoff が 0 なのでセクションは無い
        ((DecodedVirtual)Child(body, "section_count")).Value.Should().Be(0L);
        ((DecodedArray)Child(body, "section_headers")).Elements.Should().BeEmpty();
    }

    [Fact]
    public void ElfFormat_Ident_DecodesCorrectly()
    {
        var ident = Child(Decode(ElfTestDataGenerator.CreateMinimalElf64()), "e_ident");

        ((DecodedBytes)Child(ident, "magic")).ValidationPassed.Should().BeTrue();
        var eiClass = (DecodedInteger)Child(ident, "ei_class");
        eiClass.Value.Should().Be(2);
        eiClass.EnumLabel.Should().Be("ELFCLASS64");
        var eiData = (DecodedInteger)Child(ident, "ei_data");
        eiData.Value.Should().Be(1);
        eiData.EnumLabel.Should().Be("ELFDATA2LSB");
        Child(ident, "ei_version").Validation!.Passed.Should().BeTrue();
    }

    [Fact]
    public void ElfFormat_Header_DecodesCorrectly()
    {
        var header = Child(Child(Decode(ElfTestDataGenerator.CreateMinimalElf64()), "body"), "header");

        ((DecodedInteger)Child(header, "e_type")).EnumLabel.Should().Be("ET_EXEC");
        ((DecodedInteger)Child(header, "e_machine")).EnumLabel.Should().Be("EM_X86_64");
        ((DecodedInteger)Child(header, "e_phnum")).Value.Should().Be(1);
    }

    [Fact]
    public void ElfFormat_ProgramHeaders_DecodesCorrectly()
    {
        var body = Child(Decode(ElfTestDataGenerator.CreateMinimalElf64()), "body");
        var phdr = ((DecodedArray)Child(body, "program_headers")).Elements.Single();

        var pType = (DecodedInteger)Child(phdr, "p_type");
        pType.Value.Should().Be(1);
        pType.EnumLabel.Should().Be("PT_LOAD");

        var pFlags = (DecodedFlags)Child(phdr, "p_flags");
        pFlags.RawValue.Should().Be(5);
        pFlags.FlagStates.Should().HaveCount(3);
        pFlags.FlagStates.Should().Contain(f => f.Name == "PF_X" && f.IsSet);
        pFlags.FlagStates.Should().Contain(f => f.Name == "PF_W" && !f.IsSet);
        pFlags.FlagStates.Should().Contain(f => f.Name == "PF_R" && f.IsSet);
    }

    [Fact]
    public void ElfFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(ElfTestDataGenerator.CreateMinimalElf64()));

        output.Should().Contain("ELF");
        output.Should().Contain("e_ident");
        output.Should().Contain("ELFCLASS64");
        output.Should().Contain("ELFDATA2LSB");
        output.Should().Contain("ET_EXEC");
        output.Should().Contain("EM_X86_64");
        output.Should().Contain("PT_LOAD");
        output.Should().Contain("program_headers");
    }

    [Fact]
    public void ElfFormat_BigEndian64_DecodesCorrectly()
    {
        var decoded = Decode(ElfTestDataGenerator.CreateMinimalElf64BigEndian());

        ((DecodedInteger)Child(Child(decoded, "e_ident"), "ei_data")).EnumLabel.Should().Be("ELFDATA2MSB");
        var body = Child(decoded, "body");
        var header = Child(body, "header");
        ((DecodedInteger)Child(header, "e_type")).EnumLabel.Should().Be("ET_EXEC");
        ((DecodedInteger)Child(header, "e_machine")).Value.Should().Be(62);
        var phdr = ((DecodedArray)Child(body, "program_headers")).Elements.Single();
        ((DecodedInteger)Child(phdr, "p_type")).EnumLabel.Should().Be("PT_LOAD");
    }

    [Fact]
    public void ElfFormat_BigEndian32_DecodesCorrectly()
    {
        var decoded = Decode(ElfTestDataGenerator.CreateMinimalElf32BigEndian());

        var ident = Child(decoded, "e_ident");
        ((DecodedInteger)Child(ident, "ei_class")).EnumLabel.Should().Be("ELFCLASS32");
        ((DecodedInteger)Child(ident, "ei_data")).EnumLabel.Should().Be("ELFDATA2MSB");
        var body = Child(decoded, "body");
        ((DecodedInteger)Child(Child(body, "header"), "e_machine")).EnumLabel.Should().Be("EM_MIPS");
        var phdr = ((DecodedArray)Child(body, "program_headers")).Elements.Single();
        ((DecodedInteger)Child(phdr, "p_type")).EnumLabel.Should().Be("PT_LOAD");
    }

    [Fact]
    public void Sections_ResolveNamesSymbolsNotesDynamicAndCompressedData()
    {
        var body = Child(Decode(ElfTestDataGenerator.CreateElf64WithSections()), "body");

        var phdr = ((DecodedArray)Child(body, "program_headers")).Elements.Single();
        ((DecodedInteger)Child(phdr, "p_type")).EnumLabel.Should().Be("PT_INTERP");
        ((DecodedString)Child(phdr, "interpreter")).Value.Should().Be("/lib64/ld-linux-x86-64.so.2");

        var sections = ((DecodedArray)Child(body, "section_headers")).Elements;
        sections.Skip(1).Select(s => ((DecodedString)Child(s, "name")).Value).Should().Equal(
            ".interp", ".note.gnu.build-id", ".dynsym", ".dynstr", ".rela.dyn", ".dynamic", ".debug_str", ".shstrtab");
        DecodedNode Section(string name) => sections.Skip(1).Single(s => ((DecodedString)Child(s, "name")).Value == name);

        var note = ((DecodedArray)Child(Child(Section(".note.gnu.build-id"), "contents"), "notes")).Elements.Single();
        ((DecodedVirtual)Child(note, "gnu_type")).EnumLabel.Should().Be("NT_GNU_BUILD_ID");
        ((DecodedBytes)Child(note, "desc")).RawBytes.Length.Should().Be(20);

        var symbols = ((DecodedArray)Child(Child(Section(".dynsym"), "contents"), "symbols")).Elements;
        symbols.Should().HaveCount(3);
        ((DecodedString)Child(symbols[1], "name")).Value.Should().Be("add");
        ((DecodedVirtual)Child(symbols[1], "symbol_type")).EnumLabel.Should().Be("STT_FUNC");
        ((DecodedVirtual)Child(symbols[1], "bind")).EnumLabel.Should().Be("STB_GLOBAL");
        ((DecodedString)Child(symbols[2], "name")).Value.Should().Be("counter");
        ((DecodedVirtual)Child(symbols[2], "bind")).EnumLabel.Should().Be("STB_WEAK");
        ((DecodedVirtual)Child(symbols[2], "visibility")).EnumLabel.Should().Be("STV_HIDDEN");
        ((DecodedInteger)Child(symbols[2], "st_shndx")).EnumLabel.Should().Be("SHN_ABS");

        var relocation = ((DecodedArray)Child(Child(Section(".rela.dyn"), "contents"), "relocations")).Elements.Single();
        ((DecodedVirtual)Child(relocation, "symbol_index")).Value.Should().Be(2L);
        ((DecodedVirtual)Child(relocation, "relocation_type")).Value.Should().Be(6L);
        ((DecodedInteger)Child(relocation, "r_addend")).Value.Should().Be(-8);

        var dynamic = ((DecodedArray)Child(Child(Section(".dynamic"), "contents"), "entries")).Elements;
        ((DecodedInteger)Child(dynamic[0], "d_tag")).EnumLabel.Should().Be("DT_NEEDED");
        ((DecodedString)Child(dynamic[0], "string_value")).Value.Should().Be("libc.so.6");
        ((DecodedInteger)Child(dynamic[2], "d_tag")).EnumLabel.Should().Be("DT_NULL");

        var compressed = Child(Section(".debug_str"), "contents");
        ((DecodedInteger)Child(compressed, "ch_type")).EnumLabel.Should().Be("ELFCOMPRESS_ZLIB");
        var content = (DecodedCompressed)Child(Child(compressed, "data"), "content");
        content.DecompressedSize.Should().Be(130);
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(ElfFormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
