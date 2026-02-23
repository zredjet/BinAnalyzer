using BinAnalyzer.Core.Expressions;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

/// <summary>
/// REQ-142: state / state_if / state_default のYAMLパース
/// </summary>
public class StatefulVariableParsingTests
{
    [Fact]
    public void YamlParsing_State_MapsCorrectly()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: status
                  type: uint8
                  state: "running_status"
                  state_if: "{status & 0x80}"
                  state_default: 0
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.State.Should().Be("running_status");
        field.StateIf.Should().NotBeNull();
        field.StateIf!.OriginalText.Should().Be("{status & 0x80}");
        field.StateDefault.Should().Be(0);
    }

    [Fact]
    public void YamlParsing_StateDefault_Integer()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: val
                  type: uint8
                  state: "counter"
                  state_default: 255
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.State.Should().Be("counter");
        field.StateDefault.Should().Be(255);
        field.StateIf.Should().BeNull();
    }

    [Fact]
    public void YamlParsing_StateOnly_NoCondition()
    {
        var yaml = """
            name: Test
            root: main
            structs:
              main:
                - name: val
                  type: uint8
                  state: "my_var"
            """;

        var loader = new YamlFormatLoader();
        var format = loader.LoadFromString(yaml);

        var field = format.Structs["main"].Fields[0];
        field.State.Should().Be("my_var");
        field.StateIf.Should().BeNull();
        field.StateDefault.Should().BeNull();
    }
}
