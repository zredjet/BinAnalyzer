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
        var decoded = Decode(JavaClassTestDataGenerator.CreateMinimalJavaClass());

        decoded.Name.Should().Be("JavaClass");
        decoded.Children.Select(c => c.Name).Should().StartWith(
            ["magic", "minor_version", "major_version", "constant_pool_count", "next_is_unusable", "constant_pool"]);
    }

    [Fact]
    public void JavaClassFormat_Magic_DecodesCorrectly()
    {
        ((DecodedBytes)Child(Decode(JavaClassTestDataGenerator.CreateMinimalJavaClass()), "magic")).ValidationPassed.Should().BeTrue();
    }

    [Fact]
    public void JavaClassFormat_Version_DecodesCorrectly()
    {
        var majorVersion = (DecodedInteger)Child(Decode(JavaClassTestDataGenerator.CreateMinimalJavaClass()), "major_version");
        majorVersion.Value.Should().Be(61);
        majorVersion.EnumLabel.Should().Be("Java_17");
    }

    [Fact]
    public void JavaClassFormat_ConstantPool_DecodesCorrectly()
    {
        var decoded = Decode(JavaClassTestDataGenerator.CreateMinimalJavaClass());

        ((DecodedInteger)Child(decoded, "constant_pool_count")).Value.Should().Be(3);
        var cp = ((DecodedArray)Child(decoded, "constant_pool")).Elements;
        cp.Should().HaveCount(2);
        ((DecodedInteger)Child(cp[0], "tag")).EnumLabel.Should().Be("Class");
        ((DecodedInteger)Child(cp[1], "tag")).EnumLabel.Should().Be("Utf8");
    }

    [Fact]
    public void JavaClassFormat_TreeOutput_ContainsExpectedElements()
    {
        var output = new TreeOutputFormatter().Format(Decode(JavaClassTestDataGenerator.CreateMinimalJavaClass()));

        output.Should().Contain("JavaClass");
        output.Should().Contain("magic");
        output.Should().Contain("Java_17");
        output.Should().Contain("constant_pool");
        output.Should().Contain("Class");
        output.Should().Contain("Utf8");
    }

    [Fact]
    public void WideConstants_TakeTwoSlotsAndNamesResolveThroughTheConstantPool()
    {
        // 以前は Long / Double の 2 スロット目を数えず、コンスタントプールの後ろを 2 エントリ分読みすぎていた
        var decoded = Decode(JavaClassTestDataGenerator.CreateClassWithWideConstantsAndCode());

        var cp = ((DecodedArray)Child(decoded, "constant_pool")).Elements;
        cp.Should().HaveCount(19);
        ((DecodedInteger)Child(cp[7], "tag")).EnumLabel.Should().Be("Long");
        ((DecodedInteger)Child(Child(cp[7], "info"), "value")).Value.Should().Be(1234567890123L);
        ((DecodedVirtual)Child(cp[8], "unusable")).Value.Should().Be(1L);
        ((DecodedStruct)cp[8]).Children.Should().NotContain(c => c.Name == "tag");
        ((DecodedInteger)Child(cp[9], "tag")).EnumLabel.Should().Be("Double");
        ((DecodedVirtual)Child(cp[10], "unusable")).Value.Should().Be(1L);

        ((DecodedVirtual)Child(decoded, "this_class_name")).Value.Should().Be("Gen");
        ((DecodedVirtual)Child(decoded, "super_class_name")).Value.Should().Be("java/lang/Object");

        var fields = ((DecodedArray)Child(decoded, "fields")).Elements;
        fields.Select(f => ((DecodedVirtual)Child(f, "name")).Value).Should().Equal("BIG", "PI");
        var constantValue = ((DecodedArray)Child(fields[0], "attributes")).Elements.Single();
        ((DecodedVirtual)Child(constantValue, "attribute_name")).Value.Should().Be("ConstantValue");
        ((DecodedInteger)Child(Child(constantValue, "info"), "constantvalue_index")).Value.Should().Be(8);

        var main = ((DecodedArray)Child(decoded, "methods")).Elements.Single();
        ((DecodedVirtual)Child(main, "name")).Value.Should().Be("main");
        ((DecodedVirtual)Child(main, "descriptor")).Value.Should().Be("([Ljava/lang/String;)V");
        var code = Child(((DecodedArray)Child(main, "attributes")).Elements.Single(), "info");
        ((DecodedBytes)Child(code, "code")).RawBytes.ToArray().Should().Equal(0xB1);
        var lineNumbers = ((DecodedArray)Child(code, "attributes")).Elements.Single();
        ((DecodedVirtual)Child(lineNumbers, "attribute_name")).Value.Should().Be("LineNumberTable");
        var line = ((DecodedArray)Child(Child(lineNumbers, "info"), "line_number_table")).Elements.Single();
        ((DecodedInteger)Child(line, "line_number")).Value.Should().Be(3);

        var sourceFile = ((DecodedArray)Child(decoded, "attributes")).Elements.Single();
        ((DecodedVirtual)Child(Child(sourceFile, "info"), "value")).Value.Should().Be("Gen.java");
    }

    private static DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(JavaClassFormatPath));

    private static DecodedNode Child(DecodedNode node, string name) =>
        ((DecodedStruct)node).Children.First(c => c.Name == name);
}
