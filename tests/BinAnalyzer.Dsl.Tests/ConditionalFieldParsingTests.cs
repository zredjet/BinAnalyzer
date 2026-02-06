using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

public class ConditionalFieldParsingTests
{
    [Fact]
    public void Load_FieldWithIf_ParsesCondition()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: version
                  type: uint8
                - name: extra
                  type: uint16
                  if: "{version >= 2}"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var extraField = format.Structs["root"].Fields[1];
        extraField.Condition.Should().NotBeNull();
        extraField.Condition!.OriginalText.Should().Be("{version >= 2}");
    }

    [Fact]
    public void Load_FieldWithoutIf_HasNullCondition()
    {
        var yaml = """
            name: test
            endianness: big
            root: root
            structs:
              root:
                - name: value
                  type: uint32
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["root"].Fields[0];
        field.Condition.Should().BeNull();
    }
}
