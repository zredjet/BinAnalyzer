using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class MsgpackParsingTests
{
    private static readonly string MsgpackFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "msgpack.bdef.yaml");

    [Fact]
    public void MsgpackFormat_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(MsgpackFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void MsgpackFormat_MinimalMap_DecodesKeyAndValue()
    {
        var root = Values(MsgpackTestDataGenerator.CreateMinimalMsgpack()).Single();

        root.Child("format").Str().Should().Be("fixmap");
        var entry = root.Find("entries").Elements().Single();
        Text(entry.Child("key")).Should().Be("key");
        Scalar(entry.Child("value")).Should().Be(42L);
    }

    [Fact]
    public void MsgpackFormat_Document_DecodesAllTypes()
    {
        var values = Values(MsgpackTestDataGenerator.CreateMsgpackDocument());

        values.Should().HaveCount(3);
        var map = Map(values[0]);
        map.Keys.Should().Equal("int", "str", "bin", "f", "nil", "b", "nested", "ts32", "ts64", "ts96", "arr16");
        Array(map["int"]).Select(Scalar).Should().Equal(0L, 127L, 128L, -1L, -32L, -33L, -70000L);
        Array(map["str"]).Select(Text).Should().Equal("", new string('a', 31), new string('日', 100));
        ((DecodedBytes)map["bin"].Child("data").Child("data")).RawBytes.ToArray().Should().Equal(0x00, 0x01);
        Array(map["f"]).Select(Scalar).Should().Equal(1.5, -2.25);
        Scalar(map["nil"]).Should().Be("nil");
        Array(map["b"]).Select(Scalar).Should().Equal(1L, 0L);
        Scalar(Array(Map(Map(map["nested"])["m"])["x"])[0]).Should().Be(1L);
        map["arr16"].Child("format").Str().Should().Be("array");
        Array(map["arr16"]).Should().HaveCount(20);
        Scalar(values[1]).Should().Be(42L);
        values[2].Find("ext_type").Int().Should().Be(1);
    }

    [Fact]
    public void MsgpackFormat_Timestamps_DecodeThreeForms()
    {
        var map = Map(Values(MsgpackTestDataGenerator.CreateMsgpackDocument())[0]);

        map["ts32"].Find("ext_type").Int().Should().Be(-1);
        map["ts32"].Find("seconds32").Int().Should().Be(1700000000);
        var ts64 = map["ts64"].Find("nsec_sec34");
        ts64.Bits("nanoseconds30").Should().Be(5);
        ts64.Bits("seconds34").Should().Be(1700000000);
        map["ts96"].Find("nanoseconds32").Int().Should().Be(999999999);
        map["ts96"].Find("seconds64").Int().Should().Be(-1);
    }

    [Fact]
    public void MsgpackFormat_Uint64AboveInt64_IsShownUnsigned()
    {
        var root = new BinaryDecoder().Decode(MsgpackTestDataGenerator.CreateMsgpack64BitIntegers(), new YamlFormatLoader().Load(MsgpackFormatPath));
        var items = Array(root.Child("values").Elements().Single()).Select(e => (DecodedInteger)e.Child("data").Child("value")).ToList();

        items.Select(i => i.ValueText).Should().Equal("18446744073709551615", "9223372036854775808", "-9223372036854775808");
        var json = new JsonOutputFormatter().Format(root);
        json.Should().Contain("\"value\": 18446744073709551615").And.Contain("\"hex\": \"0xFFFFFFFFFFFFFFFF\"")
            .And.Contain("\"value\": 9223372036854775808").And.Contain("\"value\": -9223372036854775808");
        new TreeOutputFormatter().Format(root).Should().Contain("value: 18446744073709551615 (0xFFFFFFFFFFFFFFFF)");
    }

    private static IReadOnlyList<DecodedNode> Values(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(MsgpackFormatPath)).Child("values").Elements();

    /// <summary>マップの値を、キーの文字列から値への辞書にする（順序を保つ）。</summary>
    private static Dictionary<string, DecodedNode> Map(DecodedNode value)
    {
        var result = new Dictionary<string, DecodedNode>();
        foreach (var entry in value.Child("data").Find("entries").Elements())
            result[Text(entry.Child("key"))] = entry.Child("value");
        return result;
    }

    private static IReadOnlyList<DecodedNode> Array(DecodedNode value) => value.Child("data").Find("elements").Elements();

    private static string Text(DecodedNode value) => value.Child("data").Child("text").Str();

    private static object Scalar(DecodedNode value) => value.Child("data").Child("value") switch
    {
        DecodedFloat f => f.Value,
        DecodedInteger i => i.Value,
        DecodedVirtual v => v.Value,
        var other => throw new InvalidOperationException(other.GetType().Name),
    };
}
