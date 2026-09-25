using BinAnalyzer.Dsl;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Dsl.Tests;

/// <summary>REQ-195: struct の <c>scope: isolated</c>。</summary>
public class IsolatedScopeParsingTests
{
    [Fact]
    public void Load_ScopeIsolated_SetsIsolatedScope()
    {
        var format = new YamlFormatLoader().LoadFromString("""
            name: test
            root: root
            structs:
              root:
                - name: child
                  type: struct
                  struct: child
              child:
                scope: isolated
                fields:
                  - name: value
                    type: uint8
            """);

        format.Structs["child"].IsolatedScope.Should().BeTrue();
        format.Structs["root"].IsolatedScope.Should().BeFalse();
        format.UnknownKeys.Should().BeEmpty();
    }

    [Fact]
    public void Load_UnknownScope_ThrowsException()
    {
        var act = () => new YamlFormatLoader().LoadFromString("""
            name: test
            root: root
            structs:
              root:
                scope: shared
                fields:
                  - name: value
                    type: uint8
            """);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown struct scope*shared*");
    }
}
