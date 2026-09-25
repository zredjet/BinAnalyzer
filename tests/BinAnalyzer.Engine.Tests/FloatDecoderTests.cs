using System.Buffers.Binary;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class FloatDecoderTests
{
    private readonly BinaryDecoder _decoder = new();

    [Fact]
    public void Decode_Float32_BigEndian()
    {
        var format = CreateFormat("main", Endianness.Big, new FieldDefinition
        {
            Name = "value",
            Type = FieldType.Float32,
        });
        var data = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(data, 3.14f);

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedFloat>().Subject;
        field.Value.Should().BeApproximately(3.14, 0.001);
        field.Precision.Should().Be(FloatPrecision.Single);
        field.Size.Should().Be(4);
    }

    [Fact]
    public void Decode_Float32_LittleEndian()
    {
        var format = CreateFormat("main", Endianness.Little, new FieldDefinition
        {
            Name = "value",
            Type = FieldType.Float32,
        });
        var data = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(data, -1.5f);

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedFloat>().Subject;
        field.Value.Should().BeApproximately(-1.5, 0.0001);
        field.Precision.Should().Be(FloatPrecision.Single);
    }

    [Fact]
    public void Decode_Float64_BigEndian()
    {
        var format = CreateFormat("main", Endianness.Big, new FieldDefinition
        {
            Name = "value",
            Type = FieldType.Float64,
        });
        var data = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(data, 2.718281828459045);

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedFloat>().Subject;
        field.Value.Should().BeApproximately(2.718281828459045, 1e-10);
        field.Precision.Should().Be(FloatPrecision.Double);
        field.Size.Should().Be(8);
    }

    [Fact]
    public void Decode_Float64_LittleEndian()
    {
        var format = CreateFormat("main", Endianness.Little, new FieldDefinition
        {
            Name = "value",
            Type = FieldType.Float64,
        });
        var data = new byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(data, 123456.789);

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedFloat>().Subject;
        field.Value.Should().BeApproximately(123456.789, 0.001);
        field.Precision.Should().Be(FloatPrecision.Double);
    }

    [Fact]
    public void Decode_Float32_SpecialValues_NaN()
    {
        var format = CreateFormat("main", Endianness.Big, new FieldDefinition
        {
            Name = "value",
            Type = FieldType.Float32,
        });
        var data = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(data, float.NaN);

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedFloat>().Subject;
        double.IsNaN(field.Value).Should().BeTrue();
    }

    [Fact]
    public void Decode_Float32_SpecialValues_Infinity()
    {
        var format = CreateFormat("main", Endianness.Big, new FieldDefinition
        {
            Name = "value",
            Type = FieldType.Float32,
        });
        var data = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(data, float.PositiveInfinity);

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedFloat>().Subject;
        double.IsPositiveInfinity(field.Value).Should().BeTrue();
    }

    [Fact]
    public void Decode_Float32_Zero()
    {
        var format = CreateFormat("main", Endianness.Big, new FieldDefinition
        {
            Name = "value",
            Type = FieldType.Float32,
        });
        var data = new byte[4]; // all zeros = 0.0f

        var result = _decoder.Decode(data, format);

        var field = result.Children[0].Should().BeOfType<DecodedFloat>().Subject;
        field.Value.Should().Be(0.0);
    }

    [Fact]
    public void Decode_Float32_VariableBinding()
    {
        // Float value should be bound as double variable for expression evaluation
        var format = CreateFormat("main", Endianness.Big,
            new FieldDefinition { Name = "threshold", Type = FieldType.Float32 },
            new FieldDefinition { Name = "tag", Type = FieldType.UInt8 });
        var data = new byte[5];
        BinaryPrimitives.WriteSingleBigEndian(data, 1.0f);
        data[4] = 0x42;

        var result = _decoder.Decode(data, format);

        result.Children.Should().HaveCount(2);
        result.Children[0].Should().BeOfType<DecodedFloat>();
        result.Children[1].Should().BeOfType<DecodedInteger>()
            .Which.Value.Should().Be(0x42);
    }

    [Fact]
    public void Diff_FloatChanged()
    {
        var left = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children = [new DecodedFloat { Name = "pi", Offset = 0, Size = 4, Value = 3.14, Precision = FloatPrecision.Single }],
        };
        var right = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 4,
            Children = [new DecodedFloat { Name = "pi", Offset = 0, Size = 4, Value = 3.15, Precision = FloatPrecision.Single }],
        };

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].FieldPath.Should().Be("pi");
        result.Entries[0].OldValue.Should().Contain("3.14");
        result.Entries[0].NewValue.Should().Contain("3.15");
    }

    [Fact]
    public void Diff_FloatIdentical()
    {
        var left = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 8,
            Children = [new DecodedFloat { Name = "e", Offset = 0, Size = 8, Value = 2.71828, Precision = FloatPrecision.Double }],
        };
        var right = new DecodedStruct
        {
            Name = "root", StructType = "root", Offset = 0, Size = 8,
            Children = [new DecodedFloat { Name = "e", Offset = 0, Size = 8, Value = 2.71828, Precision = FloatPrecision.Double }],
        };

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeFalse();
    }

    [Theory]
    [InlineData(double.NaN, double.NaN, false)]   // 同じ NaN は変わっていない（以前は == で比べて「変わった」になっていた）
    [InlineData(0.0, -0.0, true)]                 // 符号の違う 0 はバイト列が違う
    [InlineData(1.5, 1.5, false)]
    public void Diff_ComparesTheBitPattern(double left, double right, bool changed)
    {
        static DecodedStruct Root(double v) => new()
        {
            Name = "root", StructType = "root", Offset = 0, Size = 2,
            Children = [new DecodedFloat { Name = "h", Offset = 0, Size = 2, Value = v, Precision = FloatPrecision.Half }],
        };

        DiffEngine.Compare(Root(left), Root(right)).HasDifferences.Should().Be(changed);
    }

    private static FormatDefinition CreateFormat(string rootName, Endianness endianness, params FieldDefinition[] fields)
    {
        return new FormatDefinition
        {
            Name = "Test",
            Endianness = endianness,
            Enums = new Dictionary<string, EnumDefinition>(),
            Flags = new Dictionary<string, FlagsDefinition>(),
            Structs = new Dictionary<string, StructDefinition>
            {
                [rootName] = new()
                {
                    Name = rootName,
                    Fields = fields.ToList(),
                },
            },
            RootStruct = rootName,
        };
    }
}
