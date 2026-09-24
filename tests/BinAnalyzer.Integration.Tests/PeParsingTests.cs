using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class PeParsingTests
{
    private static readonly string PeFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "pe.bdef.yaml");

    [Fact]
    public void PeFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void PeFormat_DecodesWithRecovery()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        decoded.Name.Should().Be("PE");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(3);
        decoded.Children[0].Name.Should().Be("dos_header");
        decoded.Children[1].Name.Should().Be("pe_signature");
        decoded.Children[2].Name.Should().Be("coff_header");
    }

    [Fact]
    public void PeFormat_DosHeader_DecodesCorrectly()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var dosHeader = decoded.Children[0].Should().BeOfType<DecodedStruct>().Subject;
        var eMagic = dosHeader.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        eMagic.ValidationPassed.Should().BeTrue();

        var eLfanew = dosHeader.Children.Last().Should().BeOfType<DecodedInteger>().Subject;
        eLfanew.Name.Should().Be("e_lfanew");
        eLfanew.Value.Should().Be(64);
    }

    [Fact]
    public void PeFormat_CoffHeader_DecodesCorrectly()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var decoded = result.Root;

        var peSignature = decoded.Children[1].Should().BeOfType<DecodedBytes>().Subject;
        peSignature.ValidationPassed.Should().BeTrue();

        var coffHeader = decoded.Children[2].Should().BeOfType<DecodedStruct>().Subject;
        var machine = coffHeader.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        machine.EnumLabel.Should().Be("IMAGE_FILE_MACHINE_AMD64");

        var numSections = coffHeader.Children[1].Should().BeOfType<DecodedInteger>().Subject;
        numSections.Value.Should().Be(1);
    }

    [Fact]
    public void PeFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = PeTestDataGenerator.CreateMinimalPe();
        var format = new YamlFormatLoader().Load(PeFormatPath);
        var result = new BinaryDecoder().DecodeWithRecovery(data, format, ErrorMode.Continue);
        var output = new TreeOutputFormatter().Format(result.Root);

        output.Should().Contain("PE");
        output.Should().Contain("dos_header");
        output.Should().Contain("pe_signature");
        output.Should().Contain("coff_header");
        output.Should().Contain("IMAGE_FILE_MACHINE_AMD64");
    }

    [Fact]
    public void Pe32PlusDll_DecodesDirectoriesInsideTheirSections()
    {
        var decoded = Decode(PeTestDataGenerator.CreatePe32PlusDll());

        ((DecodedVirtual)Child(decoded, "is_pe32plus")).Value.Should().Be(1L);
        var sections = ((DecodedArray)Child(decoded, "sections")).Elements;
        var rdata = sections[0];
        ((DecodedString)Child(rdata, "name")).Value.Should().StartWith(".rdata");

        var exports = Child(rdata, "export_directory");
        ((DecodedString)Child(exports, "dll_name")).Value.Should().Be("gen.dll");
        ((DecodedArray)Child(exports, "names")).Elements.Select(n => ((DecodedString)Child(n, "name")).Value).Should().Equal("Alpha", "Beta");

        var descriptor = ((DecodedArray)Child(Child(rdata, "import_directory"), "descriptors")).Elements[0];
        ((DecodedString)Child(descriptor, "dll_name")).Value.Should().Be("KERNEL32.dll");
        var thunks = ((DecodedArray)Child(Child(descriptor, "functions"), "entries")).Elements;
        thunks.Should().HaveCount(3);
        var byName = Child(thunks[0], "hint_name");
        ((DecodedInteger)Child(byName, "hint")).Value.Should().Be(0x123);
        ((DecodedString)Child(byName, "name")).Value.Should().Be("ExitProcess");
        ((DecodedVirtual)Child(thunks[1], "by_ordinal")).Value.Should().Be(1L);
        ((DecodedVirtual)Child(thunks[1], "ordinal")).Value.Should().Be(5L);

        var debug = ((DecodedArray)Child(Child(rdata, "debug_directory"), "entries")).Elements.Single();
        ((DecodedInteger)Child(debug, "debug_type")).EnumLabel.Should().Be("IMAGE_DEBUG_TYPE_CODEVIEW");
        ((DecodedString)Child(Child(debug, "codeview"), "pdb_path")).Value.Should().Be("C:\\gen.pdb");

        var block = ((DecodedArray)Child(Child(sections[1], "base_relocations"), "blocks")).Elements.Single();
        var entry = (DecodedBitfield)Child(((DecodedArray)Child(block, "entries")).Elements[0], "entry");
        entry.Fields.Single(f => f.Name == "relocation_type").EnumLabel.Should().Be("IMAGE_REL_BASED_DIR64");

        var certificate = ((DecodedArray)Child(Child(decoded, "certificate_table"), "certificates")).Elements.Single();
        ((DecodedInteger)Child(certificate, "certificate_type")).EnumLabel.Should().Be("WIN_CERT_TYPE_PKCS_SIGNED_DATA");
        ((DecodedBytes)Child(certificate, "padding")).RawBytes.Length.Should().Be(3);
    }

    [Fact]
    public void ManagedPe32_DecodesClrHeaderMetadataAndThirtyTwoBitImports()
    {
        var decoded = Decode(PeTestDataGenerator.CreateManagedPe32());

        ((DecodedVirtual)Child(decoded, "is_pe32plus")).Value.Should().Be(0L);
        var text = ((DecodedArray)Child(decoded, "sections")).Elements.Single();
        var clr = Child(text, "clr_header");
        ((DecodedFlags)Child(clr, "flags")).FlagStates.Should().Contain(f => f.Name == "ILONLY" && f.IsSet);
        var metadata = Child(clr, "metadata_root");
        Child(metadata, "signature").Validation!.Passed.Should().BeTrue();
        ((DecodedString)Child(metadata, "version")).Value.TrimEnd('\0').Should().Be("v4.0.30319");
        ((DecodedArray)Child(metadata, "streams")).Elements.Select(s => ((DecodedString)Child(s, "name")).Value).Should().Equal("#~", "#Strings");

        var descriptor = ((DecodedArray)Child(Child(text, "import_directory"), "descriptors")).Elements[0];
        ((DecodedString)Child(descriptor, "dll_name")).Value.Should().Be("mscoree.dll");
        var thunk = ((DecodedArray)Child(Child(descriptor, "functions"), "entries")).Elements[0];
        ((DecodedInteger)Child(thunk, "thunk")).Size.Should().Be(4);
        ((DecodedString)Child(Child(thunk, "hint_name"), "name")).Value.Should().Be("_CorDllMain");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(PeFormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
