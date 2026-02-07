using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class JavaClassParsingTests
{
    private static readonly string JavaClassFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "java-class.bdef.yaml");

    [Fact]
    public void JavaClassFormat_LoadsWithoutErrors()
    {
        var format = new YamlFormatLoader().Load(JavaClassFormatPath);
        var result = FormatValidator.Validate(format);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void JavaClassFormat_DecodesMinimalJavaClass()
    {
        var data = JavaClassTestDataGenerator.CreateMinimalJavaClass();
        var format = new YamlFormatLoader().Load(JavaClassFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        decoded.Name.Should().Be("JavaClass");
        decoded.Children.Should().HaveCountGreaterThanOrEqualTo(8);
        decoded.Children[0].Name.Should().Be("magic");
        decoded.Children[1].Name.Should().Be("minor_version");
        decoded.Children[2].Name.Should().Be("major_version");
        decoded.Children[3].Name.Should().Be("constant_pool_count");
        decoded.Children[4].Name.Should().Be("constant_pool");
    }

    [Fact]
    public void JavaClassFormat_Magic_DecodesCorrectly()
    {
        var data = JavaClassTestDataGenerator.CreateMinimalJavaClass();
        var format = new YamlFormatLoader().Load(JavaClassFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var magic = decoded.Children[0].Should().BeOfType<DecodedBytes>().Subject;
        magic.ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void JavaClassFormat_Version_DecodesCorrectly()
    {
        var data = JavaClassTestDataGenerator.CreateMinimalJavaClass();
        var format = new YamlFormatLoader().Load(JavaClassFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var majorVersion = decoded.Children[2].Should().BeOfType<DecodedInteger>().Subject;
        majorVersion.Value.Should().Be(61);
        majorVersion.EnumLabel.Should().Be("Java_17");
    }

    [Fact]
    public void JavaClassFormat_ConstantPool_DecodesCorrectly()
    {
        var data = JavaClassTestDataGenerator.CreateMinimalJavaClass();
        var format = new YamlFormatLoader().Load(JavaClassFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);

        var cpCount = decoded.Children[3].Should().BeOfType<DecodedInteger>().Subject;
        cpCount.Value.Should().Be(3);

        var cp = decoded.Children[4].Should().BeOfType<DecodedArray>().Subject;
        cp.Elements.Should().HaveCount(2);

        // First entry: CONSTANT_Class
        var classEntry = cp.Elements[0].Should().BeOfType<DecodedStruct>().Subject;
        var tag = classEntry.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        tag.Value.Should().Be(7);
        tag.EnumLabel.Should().Be("Class");

        // Second entry: CONSTANT_Utf8
        var utf8Entry = cp.Elements[1].Should().BeOfType<DecodedStruct>().Subject;
        var utf8Tag = utf8Entry.Children[0].Should().BeOfType<DecodedInteger>().Subject;
        utf8Tag.Value.Should().Be(1);
        utf8Tag.EnumLabel.Should().Be("Utf8");
    }

    [Fact]
    public void JavaClassFormat_TreeOutput_ContainsExpectedElements()
    {
        var data = JavaClassTestDataGenerator.CreateMinimalJavaClass();
        var format = new YamlFormatLoader().Load(JavaClassFormatPath);
        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("JavaClass");
        output.Should().Contain("magic");
        output.Should().Contain("Java_17");
        output.Should().Contain("constant_pool");
        output.Should().Contain("Class");
        output.Should().Contain("Utf8");
    }
}
