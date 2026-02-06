using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Diff;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Engine.Tests;

public class DiffEngineTests
{
    [Fact]
    public void IdenticalStructs_NoDifferences()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeFalse();
        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public void IntegerValueChanged_ReportsChanged()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 200),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Changed);
        result.Entries[0].FieldPath.Should().Be("width");
        result.Entries[0].OldValue.Should().Contain("100");
        result.Entries[0].NewValue.Should().Contain("200");
    }

    [Fact]
    public void StringValueChanged_ReportsChanged()
    {
        var left = CreateStruct("root", [
            CreateString("name", "hello"),
        ]);
        var right = CreateStruct("root", [
            CreateString("name", "world"),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Changed);
        result.Entries[0].FieldPath.Should().Be("name");
        result.Entries[0].OldValue.Should().Contain("hello");
        result.Entries[0].NewValue.Should().Contain("world");
    }

    [Fact]
    public void NestedStruct_ReportsNestedPath()
    {
        var left = CreateStruct("root", [
            CreateStruct("header", [
                CreateInteger("width", 100),
            ]),
        ]);
        var right = CreateStruct("root", [
            CreateStruct("header", [
                CreateInteger("width", 200),
            ]),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Entries.Should().HaveCount(1);
        result.Entries[0].FieldPath.Should().Be("header.width");
    }

    [Fact]
    public void ArrayElementCountDifferent_ReportsAddedRemoved()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateInteger("item", 1),
                CreateInteger("item", 2),
                CreateInteger("item", 3),
            ]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateInteger("item", 1),
            ]),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Removed && e.FieldPath == "items[1]");
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Removed && e.FieldPath == "items[2]");
    }

    [Fact]
    public void ArrayElementAdded_ReportsAdded()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateInteger("item", 1),
            ]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateInteger("item", 1),
                CreateInteger("item", 2),
            ]),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Entries.Should().Contain(e => e.Kind == DiffKind.Added && e.FieldPath == "items[1]");
    }

    [Fact]
    public void FieldOnlyInLeft_ReportsRemoved()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 100),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Entries.Should().Contain(e => e.Kind == DiffKind.Removed && e.FieldPath == "height");
    }

    [Fact]
    public void FieldOnlyInRight_ReportsAdded()
    {
        var left = CreateStruct("root", [
            CreateInteger("width", 100),
        ]);
        var right = CreateStruct("root", [
            CreateInteger("width", 100),
            CreateInteger("height", 200),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Entries.Should().Contain(e => e.Kind == DiffKind.Added && e.FieldPath == "height");
    }

    [Fact]
    public void IntegerWithEnumLabel_IncludesLabelInValue()
    {
        var left = CreateStruct("root", [
            new DecodedInteger { Name = "color_type", Offset = 0, Size = 1, Value = 2, EnumLabel = "truecolor" },
        ]);
        var right = CreateStruct("root", [
            new DecodedInteger { Name = "color_type", Offset = 0, Size = 1, Value = 6, EnumLabel = "truecolor_alpha" },
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Entries.Should().HaveCount(1);
        result.Entries[0].OldValue.Should().Contain("truecolor");
        result.Entries[0].NewValue.Should().Contain("truecolor_alpha");
    }

    [Fact]
    public void BytesChanged_ReportsChanged()
    {
        var left = CreateStruct("root", [
            new DecodedBytes { Name = "sig", Offset = 0, Size = 4, RawBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 } },
        ]);
        var right = CreateStruct("root", [
            new DecodedBytes { Name = "sig", Offset = 0, Size = 4, RawBytes = new byte[] { 0x42, 0x4D, 0x00, 0x00 } },
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries[0].FieldPath.Should().Be("sig");
    }

    private static DecodedStruct CreateStruct(string name, List<DecodedNode> children)
    {
        return new DecodedStruct
        {
            Name = name,
            StructType = name,
            Offset = 0,
            Size = 0,
            Children = children,
        };
    }

    private static DecodedInteger CreateInteger(string name, long value)
    {
        return new DecodedInteger { Name = name, Offset = 0, Size = 4, Value = value };
    }

    private static DecodedString CreateString(string name, string value)
    {
        return new DecodedString { Name = name, Offset = 0, Size = value.Length, Value = value, Encoding = "ascii" };
    }

    private static DecodedArray CreateArray(string name, List<DecodedNode> elements)
    {
        return new DecodedArray { Name = name, Offset = 0, Size = 0, Elements = elements };
    }
}
