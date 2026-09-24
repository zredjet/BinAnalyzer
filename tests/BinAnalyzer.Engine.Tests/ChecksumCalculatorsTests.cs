using System.Text;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

/// <summary>REQ-187: チェックサムの名前（Core）と計算の実装（Engine）が 1 対 1 で、計算できないものは ✓ にしない。</summary>
public class ChecksumCalculatorsTests
{
    [Fact]
    public void IntegerCalculators_CoverExactlyTheIntegerAlgorithms()
    {
        ChecksumCalculators.Integer.Keys.Should().BeEquivalentTo(ChecksumAlgorithms.IntegerAlgorithms);
    }

    [Fact]
    public void HashCalculators_CoverExactlyTheHashAlgorithms()
    {
        ChecksumCalculators.Hash.Keys.Should().BeEquivalentTo(ChecksumAlgorithms.HashAlgorithms);
    }

    [Fact]
    public void EveryCalculator_Runs()
    {
        var data = Encoding.ASCII.GetBytes("123456789");
        foreach (var (name, compute) in ChecksumCalculators.Integer)
            compute(data).Should().NotBe(0, name);
        foreach (var (name, compute) in ChecksumCalculators.Hash)
            compute(data).Should().NotBeEmpty(name);
        // 既知の検査値（CRC-32/ISO-HDLC）で表の対応が取り違えられていないことを確かめる
        ChecksumCalculators.Integer[ChecksumAlgorithms.Crc32](data).Should().Be(0xCBF43926);
        ChecksumCalculators.Integer["CRC32"](data).Should().Be(0xCBF43926, "名前は大文字小文字を区別しない");
    }

    private static DecodedStruct Decode(FieldType type, string algorithm, byte[] data) =>
        new BinaryDecoder().Decode(data, new FormatDefinition
        {
            Name = "t",
            Endianness = Endianness.Big,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                ["root"] = new()
                {
                    Name = "root",
                    Fields =
                    [
                        new FieldDefinition { Name = "body", Type = FieldType.Bytes, Size = 4 },
                        new FieldDefinition
                        {
                            Name = "sum", Type = type, Size = type == FieldType.Bytes ? 4 : null,
                            Checksum = new ChecksumSpec { Algorithm = algorithm, FieldNames = ["body"] },
                        },
                    ],
                },
            },
            RootStruct = "root",
        });

    private static readonly byte[] Garbage = [0x41, 0x42, 0x43, 0x44, 0xDE, 0xAD, 0xBE, 0xEF];

    [Theory]
    [InlineData(FieldType.UInt32, "crc32c")]
    [InlineData(FieldType.UInt32, "sha256")]
    [InlineData(FieldType.Bytes, "sha3")]
    public void AlgorithmWithoutCalculator_IsNotVerified(FieldType type, string algorithm)
    {
        // 以前は「常に一致」として ✓ を出していた（未知の名前、整数フィールドに付けたハッシュ系）
        var sum = Decode(type, algorithm, Garbage).Children[1];

        var valid = sum switch
        {
            DecodedInteger i => i.ChecksumValid,
            DecodedBytes b => b.ChecksumValid,
            _ => throw new InvalidOperationException(),
        };
        valid.Should().BeNull();
    }

    [Fact]
    public void KnownAlgorithm_WrongValue_IsInvalid()
    {
        var sum = (DecodedInteger)Decode(FieldType.UInt32, "crc32", Garbage).Children[1];

        sum.ChecksumValid.Should().BeFalse();
        sum.ChecksumExpected.Should().Be(Crc32Calculator.Compute("ABCD"u8));
    }
}
