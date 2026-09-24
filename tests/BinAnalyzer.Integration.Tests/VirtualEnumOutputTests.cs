using System.Text.Json;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

/// <summary>REQ-186: virtual に付けた enum のラベルが、各出力で整数フィールドと同じ形で表示される。</summary>
public sealed class VirtualEnumOutputTests
{
    private const string Yaml = """
        name: t
        root: s
        enums:
          os:
            - value: 0
              label: MS-DOS
            - value: 3
              label: unix
              description: "UNIX 系"
        structs:
          s:
            - name: version
              type: uint16
            - name: os
              type: virtual
              value: "{version >> 8}"
              enum: os
        """;

    private static readonly byte[] Unix = [0x03, 0x14];
    private static readonly byte[] Dos = [0x00, 0x14];

    private static Core.Decoded.DecodedStruct Decode(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().LoadFromString(Yaml));

    [Fact]
    public void Tree_ShowsLabelAndDescription()
    {
        new TreeOutputFormatter().Format(Decode(Unix)).Should().Contain("os: = 3 \"unix\" - UNIX 系");
    }

    [Fact]
    public void Json_HasEnumLabelAndDescription()
    {
        var json = JsonDocument.Parse(new JsonOutputFormatter().Format(Decode(Unix)));
        var os = json.RootElement.GetProperty("children").GetProperty("os");

        os.GetProperty("value").GetInt64().Should().Be(3);
        os.GetProperty("enum_label").GetString().Should().Be("unix");
        os.GetProperty("enum_description").GetString().Should().Be("UNIX 系");
    }

    [Fact]
    public void Html_ShowsLabel()
    {
        var html = new HtmlOutputFormatter().Format(Decode(Unix));

        html.Should().Contain("<span class=\"value enum\">\"unix\"</span>");
    }

    [Fact]
    public void Diff_ShowsLabelsOnBothSides()
    {
        var diff = DiffEngine.Compare(Decode(Dos), Decode(Unix));

        var entry = diff.Entries.Should().ContainSingle(e => e.FieldPath.EndsWith("os")).Subject;
        entry.OldValue.Should().Be("0 \"MS-DOS\"");
        entry.NewValue.Should().Be("3 \"unix\"");
    }
}
