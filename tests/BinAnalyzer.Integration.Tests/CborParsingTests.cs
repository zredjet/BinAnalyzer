using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Validation;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class CborParsingTests
{
    private static readonly string CborFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "cbor.bdef.yaml");

    [Fact]
    public void CborFormat_LoadsWithoutErrors()
    {
        var result = FormatValidator.Validate(new YamlFormatLoader().Load(CborFormatPath));
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void CborFormat_MinimalMap_DecodesKeyAndValue()
    {
        var root = Values(CborTestDataGenerator.CreateMinimalCbor()).Single();

        ((DecodedBitfield)root.Child("initial_byte")).Fields.Should().Contain(f => f.Name == "major_type" && f.EnumLabel == "map");
        var entry = root.Child("content").Child("entries").Elements().Single();
        Convert(entry.Child("key")).Should().Be("key");
        Convert(entry.Child("value")).Should().Be(42L);
    }

    [Fact]
    public void CborFormat_Rfc8949Examples_MatchDiagnosticNotation()
    {
        var values = Values(CborTestDataGenerator.CreateCborRfc8949Examples()).Select(Convert).ToList();

        values.Should().HaveCount(33);
        values.Take(8).Should().Equal(0L, 23L, 24L, 1000L, 1000000L, 1000000000000L, -1L, -1000L);
        values[8].Should().Be(1.0);                              // 半精度（f9 3c00）
        values[9].Should().Be(-4.0);                             // 半精度（f9 c400）
        values[10].Should().Be(100000.0);
        values[11].Should().Be(1.1);
        values.Skip(12).Take(5).Should().Equal("false", "true", "null", "undefined", "simple(24)");
        values[17].Should().Be("0(2013-03-21T20:04:00Z)");
        values[18].Should().Be("1(1363896240)");
        values[19].Should().Be("23(h'01020304')");
        values[20].Should().Be("24(h'6449455446')");
        values[21].Should().Be("32(http://www.example.com)");
        values.Skip(22).Take(4).Should().Equal("h'01020304'", "IETF", "ü", "𐅑");
        values[26].Should().Be("[1, [2, 3], [4, 5]]");
        values[27].Should().Be("{a: 1, b: [2, 3]}");
    }

    [Fact]
    public void CborFormat_Rfc8949HalfFloatExamples_AreDecodedAsValues()
    {
        var floats = Values(CborTestDataGenerator.CreateCborHalfFloatExamples())
            .Select(v => (DecodedFloat)v.Child("content").Child("float16_value")).ToList();

        floats.Should().OnlyContain(f => f.Precision == FloatPrecision.Half && f.Size == 2);
        floats.Select(f => f.Value).Should().Equal(
            0.0, -0.0, 1.0, 1.5, 65504.0, 5.960464477539063e-8, 0.00006103515625, -4.0,
            double.PositiveInfinity, double.NaN, double.NegativeInfinity);
        double.IsNegative(floats[1].Value).Should().BeTrue("-0.0 の符号を保つ");
    }

    [Fact]
    public void CborFormat_HalfFloat_IsShownAsFloat16InTheOutputs()
    {
        var root = new BinaryDecoder().Decode(CborTestDataGenerator.CreateCborHalfFloatExamples(), new YamlFormatLoader().Load(CborFormatPath));

        var json = new JsonOutputFormatter().Format(root);
        json.Should().Contain("\"type\": \"float16\"");
        json.Should().Contain("65504");
        // ツリーの数値は現在のカルチャで書く（ja-JP の無限大は「∞」、インバリアントは「Infinity」）ので、カルチャを固定して確かめる
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        string tree;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            tree = new TreeOutputFormatter().Format(root);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
        tree.Should().Contain("float16_value: 65504").And.Contain("float16_value: -Infinity").And.Contain("float16_value: NaN");
        new CsvOutputFormatter().Format(root).Should().Contain("float16");
    }

    [Fact]
    public void CborFormat_IndefiniteLength_ReadsUntilBreak()
    {
        var values = Values(CborTestDataGenerator.CreateCborRfc8949Examples()).Select(Convert).ToList();

        values[28].Should().Be("(_ h'0102', h'030405')");
        values[29].Should().Be("(_ strea, ming)");
        values[30].Should().Be("[_ 1, [2, 3], [_ 4, 5]]");
        values[31].Should().Be("[1, [2, 3], [_ 4, 5]]");        // 定長の配列の最後が不定長
        values[32].Should().Be("{_ a, 1, b, [_ 2, 3]}");
    }

    private static IReadOnlyList<DecodedNode> Values(byte[] data) =>
        new BinaryDecoder().Decode(data, new YamlFormatLoader().Load(CborFormatPath)).Child("values").Elements();

    /// <summary>値を RFC 8949 の診断表記に近い形の文字列・数値に変換する。</summary>
    private static object Convert(DecodedNode value)
    {
        var content = (DecodedStruct)value.Child("content");
        var major = ((DecodedBitfield)value.Child("initial_byte")).Fields.First(f => f.Name == "major_type").Value;
        string Join(IEnumerable<DecodedNode> items) => string.Join(", ", items.Select(i => Format(Convert(i))));
        switch (content.StructType)
        {
            case "cbor_unsigned" or "cbor_negative": return content.Child("value").Int();
            case "cbor_text": return content.Child("text").Str();
            case "cbor_bytes": return "h'" + System.Convert.ToHexString(((DecodedBytes)content.Child("data")).RawBytes.Span).ToLowerInvariant() + "'";
            case "cbor_array": return "[" + Join(content.Child("items").Elements()) + "]";
            case "cbor_map":
                return "{" + string.Join(", ", content.Child("entries").Elements().Select(e => $"{Format(Convert(e.Child("key")))}: {Format(Convert(e.Child("value")))}")) + "}";
            case "cbor_indefinite_items":
                var items = content.Child("items").Elements().SkipLast(1);
                return major switch { 4 => "[_ " + Join(items) + "]", 5 => "{_ " + Join(items) + "}", _ => "(_ " + Join(items) + ")" };
            case "cbor_tagged": return $"{content.Child("tag").Int()}({Format(Convert(content.Child("tagged_value")))})";
            default:
                if (content.Children.FirstOrDefault(c => c.Name == "simple") is DecodedVirtual simple)
                    return simple.EnumLabel ?? $"simple({simple.Value})";
                return ((DecodedFloat)content.Children.First(c => c is DecodedFloat)).Value;
        }
    }

    private static string Format(object value) => value switch
    {
        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };
}
