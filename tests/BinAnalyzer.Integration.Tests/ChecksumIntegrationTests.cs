using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Dsl;
using BinAnalyzer.Engine;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class ChecksumIntegrationTests
{
    private static readonly string PngFormatPath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "formats", "png.bdef.yaml");

    [Fact]
    public void Png_ValidCrc_ShowsCheckmark()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var output = new TreeOutputFormatter().Format(decoded);

        // CRC検証が成功していること（✓が表示される）
        output.Should().Contain("✓ (CRC-32)");
        // CRC検証失敗（✗）がないこと
        output.Should().NotContain("✗ (CRC-32");
    }

    [Fact]
    public void Png_ValidCrc_JsonOutput_ShowsValid()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var json = new JsonOutputFormatter().Format(decoded);

        using var doc = JsonDocument.Parse(json);
        // JSON内に checksum_valid: true が含まれていること
        json.Should().Contain("\"checksum_valid\": true");
        json.Should().NotContain("\"checksum_valid\": false");
    }

    [Fact]
    public void Png_ValidCrc_JsonOutput_IncludesAlgorithm()
    {
        var pngData = PngTestDataGenerator.CreateMinimalPng();
        var format = new YamlFormatLoader().Load(PngFormatPath);
        var decoded = new BinaryDecoder().Decode(pngData, format);
        var json = new JsonOutputFormatter().Format(decoded);

        json.Should().Contain("\"checksum_algorithm\": \"crc32\"");
    }

    [Fact]
    public void Crc16Ccitt_TreeOutput_ShowsAlgorithmName()
    {
        var payload = Encoding.ASCII.GetBytes("TEST");
        var expectedCrc = Crc16Calculator.ComputeCcitt(payload);
        var crcBytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(crcBytes, expectedCrc);

        var data = new byte[payload.Length + crcBytes.Length];
        payload.CopyTo(data, 0);
        crcBytes.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 4 },
            new FieldDefinition
            {
                Name = "crc",
                Type = FieldType.UInt16,
                Checksum = new ChecksumSpec { Algorithm = "crc16-ccitt", FieldNames = ["data"] },
            });

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("CRC-16/CCITT");
    }

    [Fact]
    public void Md5_TreeOutput_ShowsAlgorithmName()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var hash = MD5.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 16,
                Checksum = new ChecksumSpec { Algorithm = "md5", FieldNames = ["data"] },
            });

        var decoded = new BinaryDecoder().Decode(data, format);
        var output = new TreeOutputFormatter().Format(decoded);

        output.Should().Contain("MD5");
    }

    [Fact]
    public void Sha256_JsonOutput_IncludesAlgorithm()
    {
        var payload = Encoding.ASCII.GetBytes("TESTDATA");
        var hash = SHA256.HashData(payload);

        var data = new byte[payload.Length + hash.Length];
        payload.CopyTo(data, 0);
        hash.CopyTo(data, payload.Length);

        var format = MakeFormat(
            new FieldDefinition { Name = "data", Type = FieldType.Ascii, Size = 8 },
            new FieldDefinition
            {
                Name = "hash",
                Type = FieldType.Bytes,
                Size = 32,
                Checksum = new ChecksumSpec { Algorithm = "sha256", FieldNames = ["data"] },
            });

        var decoded = new BinaryDecoder().Decode(data, format);
        var json = new JsonOutputFormatter().Format(decoded);

        json.Should().Contain("\"checksum_valid\": true");
        json.Should().Contain("\"checksum_algorithm\": \"sha256\"");
    }

    private static FormatDefinition MakeFormat(params FieldDefinition[] fields) => new()
    {
        Name = "test",
        Endianness = Endianness.Big,
        Enums = new Dictionary<string, EnumDefinition>(),
        Flags = new Dictionary<string, FlagsDefinition>(),
        Structs = new Dictionary<string, StructDefinition>
        {
            ["root"] = new()
            {
                Name = "root",
                Fields = fields.ToList(),
            },
        },
        RootStruct = "root",
    };
}
