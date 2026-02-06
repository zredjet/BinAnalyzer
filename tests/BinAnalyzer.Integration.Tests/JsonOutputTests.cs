using System.Text.Json;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class JsonOutputTests
{
    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    [Fact]
    public void JsonOutput_IsValidJson()
    {
        var root = CreateSimpleStruct();
        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);

        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();
    }

    [Fact]
    public void JsonOutput_Struct_HasExpectedProperties()
    {
        var root = CreateSimpleStruct();
        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var rootEl = doc.RootElement;

        rootEl.GetProperty("_type").GetString().Should().Be("struct");
        rootEl.GetProperty("name").GetString().Should().Be("test");
        rootEl.GetProperty("struct_type").GetString().Should().Be("test_type");
        rootEl.GetProperty("offset").GetInt64().Should().Be(0);
        rootEl.GetProperty("size").GetInt64().Should().Be(10);
        rootEl.TryGetProperty("children", out _).Should().BeTrue();
    }

    [Fact]
    public void JsonOutput_Integer_HasValueAndHex()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children =
            [
                new DecodedInteger { Name = "length", Offset = 0, Size = 4, Value = 255 },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var intEl = doc.RootElement.GetProperty("children").GetProperty("length");

        intEl.GetProperty("_type").GetString().Should().Be("integer");
        intEl.GetProperty("value").GetInt64().Should().Be(255);
        intEl.GetProperty("hex").GetString().Should().Be("0xFF");
    }

    [Fact]
    public void JsonOutput_Integer_WithEnum()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 1,
            Children =
            [
                new DecodedInteger
                {
                    Name = "color", Offset = 0, Size = 1, Value = 2,
                    EnumLabel = "truecolor", EnumDescription = "RGB",
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var intEl = doc.RootElement.GetProperty("children").GetProperty("color");

        intEl.GetProperty("enum_label").GetString().Should().Be("truecolor");
        intEl.GetProperty("enum_description").GetString().Should().Be("RGB");
    }

    [Fact]
    public void JsonOutput_Bytes_HasHexAndValidation()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 3,
            Children =
            [
                new DecodedBytes
                {
                    Name = "magic", Offset = 0, Size = 3,
                    RawBytes = new byte[] { 0x89, 0x50, 0x4E },
                    ValidationPassed = true,
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var bytesEl = doc.RootElement.GetProperty("children").GetProperty("magic");

        bytesEl.GetProperty("_type").GetString().Should().Be("bytes");
        bytesEl.GetProperty("hex").GetString().Should().Be("89 50 4E");
        bytesEl.GetProperty("valid").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void JsonOutput_String_HasValueAndEncoding()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children =
            [
                new DecodedString
                {
                    Name = "type", Offset = 0, Size = 4,
                    Value = "IHDR", Encoding = "ascii",
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var strEl = doc.RootElement.GetProperty("children").GetProperty("type");

        strEl.GetProperty("_type").GetString().Should().Be("string");
        strEl.GetProperty("value").GetString().Should().Be("IHDR");
        strEl.GetProperty("encoding").GetString().Should().Be("ascii");
    }

    [Fact]
    public void JsonOutput_String_WithFlags()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children =
            [
                new DecodedString
                {
                    Name = "type", Offset = 0, Size = 4,
                    Value = "IHDR", Encoding = "ascii",
                    Flags =
                    [
                        new FlagState("ancillary", false, 5, "no"),
                        new FlagState("private", false, 4, "no"),
                    ],
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var strEl = doc.RootElement.GetProperty("children").GetProperty("type");
        var flagsEl = strEl.GetProperty("flags");

        flagsEl.GetProperty("ancillary").GetProperty("set").GetBoolean().Should().BeFalse();
        flagsEl.GetProperty("ancillary").GetProperty("meaning").GetString().Should().Be("no");
    }

    [Fact]
    public void JsonOutput_Array_HasElements()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 8,
            Children =
            [
                new DecodedArray
                {
                    Name = "items", Offset = 0, Size = 8,
                    Elements =
                    [
                        new DecodedInteger { Name = "items", Offset = 0, Size = 4, Value = 1 },
                        new DecodedInteger { Name = "items", Offset = 4, Size = 4, Value = 2 },
                    ],
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var arrayEl = doc.RootElement.GetProperty("children").GetProperty("items");

        arrayEl.GetProperty("_type").GetString().Should().Be("array");
        arrayEl.GetProperty("count").GetInt32().Should().Be(2);
        arrayEl.GetProperty("elements").GetArrayLength().Should().Be(2);
        arrayEl.GetProperty("elements")[0].GetProperty("value").GetInt64().Should().Be(1);
    }

    [Fact]
    public void JsonOutput_Flags_HasRawValueAndStates()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 1,
            Children =
            [
                new DecodedFlags
                {
                    Name = "status", Offset = 0, Size = 1, RawValue = 0x05,
                    FlagStates =
                    [
                        new FlagState("enabled", true, 0, "on"),
                        new FlagState("visible", false, 1, "hidden"),
                    ],
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var flagsEl = doc.RootElement.GetProperty("children").GetProperty("status");

        flagsEl.GetProperty("_type").GetString().Should().Be("flags");
        flagsEl.GetProperty("raw_value").GetString().Should().Be("0x5");
        flagsEl.GetProperty("flags").GetProperty("enabled").GetProperty("set").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void JsonOutput_Bitfield_HasFieldsArray()
    {
        var root = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 1,
            Children =
            [
                new DecodedBitfield
                {
                    Name = "flags", Offset = 0, Size = 1, RawValue = 0xA5,
                    Fields =
                    [
                        new BitfieldValue("high", 7, 4, 10, null, null),
                        new BitfieldValue("mode", 1, 0, 1, "normal", null),
                    ],
                },
            ],
        };

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(root);
        using var doc = JsonDocument.Parse(json);
        var bfEl = doc.RootElement.GetProperty("children").GetProperty("flags");

        bfEl.GetProperty("_type").GetString().Should().Be("bitfield");
        bfEl.GetProperty("raw_value").GetString().Should().Be("0xA5");
        var fields = bfEl.GetProperty("fields");
        fields.GetArrayLength().Should().Be(2);
        fields[0].GetProperty("name").GetString().Should().Be("high");
        fields[0].GetProperty("value").GetInt64().Should().Be(10);
        fields[0].GetProperty("bit_high").GetInt32().Should().Be(7);
        fields[0].GetProperty("bit_low").GetInt32().Should().Be(4);
        fields[1].GetProperty("enum_label").GetString().Should().Be("normal");
    }

    [Fact]
    public void JsonOutput_MinimalPng_RoundTrip()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);

        var formatter = new JsonOutputFormatter();
        var json = formatter.Format(decoded);

        // JSONとしてパース可能であること
        using var doc = JsonDocument.Parse(json);
        var rootEl = doc.RootElement;

        rootEl.GetProperty("_type").GetString().Should().Be("struct");
        rootEl.GetProperty("name").GetString().Should().Be("PNG");

        var children = rootEl.GetProperty("children");
        children.TryGetProperty("signature", out _).Should().BeTrue();
        children.TryGetProperty("chunks", out _).Should().BeTrue();

        // チャンク配列にIHDRとIENDが含まれること
        var chunks = children.GetProperty("chunks");
        chunks.GetProperty("_type").GetString().Should().Be("array");
        chunks.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(2);
    }

    private static DecodedStruct CreateSimpleStruct()
    {
        return new DecodedStruct
        {
            Name = "test",
            StructType = "test_type",
            Offset = 0,
            Size = 10,
            Children =
            [
                new DecodedInteger { Name = "field1", Offset = 0, Size = 4, Value = 42 },
            ],
        };
    }
}
