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

    [Fact]
    public void FlagsValueChanged_ReportsChanged()
    {
        var left = CreateStruct("root", [
            new DecodedFlags
            {
                Name = "flags", Offset = 0, Size = 1, RawValue = 0x01,
                FlagStates = [new FlagState("bit0", true, 0, null)],
            },
        ]);
        var right = CreateStruct("root", [
            new DecodedFlags
            {
                Name = "flags", Offset = 0, Size = 1, RawValue = 0x03,
                FlagStates = [new FlagState("bit0", true, 0, null), new FlagState("bit1", true, 1, null)],
            },
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Changed);
        result.Entries[0].FieldPath.Should().Be("flags");
        result.Entries[0].OldValue.Should().Contain("0x1");
        result.Entries[0].NewValue.Should().Contain("0x3");
    }

    [Fact]
    public void VirtualValueChanged_ReportsChanged()
    {
        var left = CreateStruct("root", [
            new DecodedVirtual { Name = "total", Offset = 0, Size = 0, Value = "100" },
        ]);
        var right = CreateStruct("root", [
            new DecodedVirtual { Name = "total", Offset = 0, Size = 0, Value = "200" },
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Changed);
        result.Entries[0].FieldPath.Should().Be("total");
        result.Entries[0].OldValue.Should().Be("100");
        result.Entries[0].NewValue.Should().Be("200");
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

    private static DecodedArray CreateArray(string name, List<DecodedNode> elements, string? diffKey = null, IReadOnlyList<string>? diffKeys = null)
    {
        var resolvedKeys = diffKeys ?? (diffKey is not null ? [diffKey] : null);
        return new DecodedArray { Name = name, Offset = 0, Size = 0, Elements = elements, DiffKey = resolvedKeys };
    }

    // --- Keyed array diff tests ---

    [Fact]
    public void KeyedArray_IdenticalElements_NoDifferences()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
            ], diffKey: "id"),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
            ], diffKey: "id"),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeFalse();
    }

    [Fact]
    public void KeyedArray_ElementRemoved_ReportsRemoved()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
                CreateStruct("entry", [CreateInteger("id", 3), CreateString("val", "C")]),
            ], diffKey: "id"),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 3), CreateString("val", "C")]),
            ], diffKey: "id"),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Removed);
        result.Entries[0].FieldPath.Should().Be("items[id=2]");
    }

    [Fact]
    public void KeyedArray_ElementAdded_ReportsAdded()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
            ], diffKey: "id"),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
            ], diffKey: "id"),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Added);
        result.Entries[0].FieldPath.Should().Be("items[id=2]");
    }

    [Fact]
    public void KeyedArray_ElementChanged_ReportsChangedWithKeyPath()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
            ], diffKey: "id"),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B2")]),
            ], diffKey: "id"),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Changed);
        result.Entries[0].FieldPath.Should().Be("items[id=2].val");
    }

    [Fact]
    public void KeyedArray_InsertionAndDeletion_MatchesByKey()
    {
        // Simulates: id=200 removed, id=350 inserted, id=400 value changed
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 100), CreateString("val", "AAA")]),
                CreateStruct("entry", [CreateInteger("id", 200), CreateString("val", "BBB")]),
                CreateStruct("entry", [CreateInteger("id", 300), CreateString("val", "CCC")]),
                CreateStruct("entry", [CreateInteger("id", 400), CreateString("val", "DDD")]),
            ], diffKey: "id"),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 100), CreateString("val", "AAA")]),
                CreateStruct("entry", [CreateInteger("id", 300), CreateString("val", "CCC")]),
                CreateStruct("entry", [CreateInteger("id", 350), CreateString("val", "NEW")]),
                CreateStruct("entry", [CreateInteger("id", 400), CreateString("val", "DDD2")]),
            ], diffKey: "id"),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();

        // id=200 removed
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Removed && e.FieldPath == "items[id=200]");
        // id=350 added
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Added && e.FieldPath == "items[id=350]");
        // id=400 val changed
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Changed && e.FieldPath == "items[id=400].val");
        // id=100 and id=300 are identical — no entries
        result.Entries.Should().NotContain(e => e.FieldPath.StartsWith("items[id=100]"));
        result.Entries.Should().NotContain(e => e.FieldPath.StartsWith("items[id=300]"));
    }

    [Fact]
    public void KeyedArray_StringKey_MatchesByStringValue()
    {
        var left = CreateStruct("root", [
            CreateArray("chunks", [
                CreateStruct("chunk", [CreateString("type", "IHDR"), CreateInteger("size", 13)]),
                CreateStruct("chunk", [CreateString("type", "IDAT"), CreateInteger("size", 100)]),
            ], diffKey: "type"),
        ]);
        var right = CreateStruct("root", [
            CreateArray("chunks", [
                CreateStruct("chunk", [CreateString("type", "IHDR"), CreateInteger("size", 13)]),
                CreateStruct("chunk", [CreateString("type", "tEXt"), CreateInteger("size", 50)]),
                CreateStruct("chunk", [CreateString("type", "IDAT"), CreateInteger("size", 200)]),
            ], diffKey: "type"),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.Entries.Should().Contain(e => e.Kind == DiffKind.Added && e.FieldPath == "chunks[type=tEXt]");
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Changed && e.FieldPath == "chunks[type=IDAT].size");
        result.Entries.Should().NotContain(e => e.FieldPath.StartsWith("chunks[type=IHDR]"));
    }

    // --- Composite key diff tests ---

    [Fact]
    public void CompositeKey_IdenticalElements_NoDifferences()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 2), CreateString("val", "B")]),
            ], diffKeys: ["group", "item"]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 2), CreateString("val", "B")]),
            ], diffKeys: ["group", "item"]),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeFalse();
    }

    [Fact]
    public void CompositeKey_ElementChanged_ReportsChangedWithCompositeKeyPath()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 2), CreateString("val", "B")]),
            ], diffKeys: ["group", "item"]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 2), CreateString("val", "B2")]),
            ], diffKeys: ["group", "item"]),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Kind.Should().Be(DiffKind.Changed);
        result.Entries[0].FieldPath.Should().Be("items[group=1,item=2].val");
    }

    [Fact]
    public void CompositeKey_ElementAddedAndRemoved_ReportsCorrectly()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("group", 2), CreateInteger("item", 1), CreateString("val", "B")]),
            ], diffKeys: ["group", "item"]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("group", 1), CreateInteger("item", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("group", 2), CreateInteger("item", 2), CreateString("val", "C")]),
            ], diffKeys: ["group", "item"]),
        ]);

        var result = DiffEngine.Compare(left, right);

        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Removed && e.FieldPath == "items[group=2,item=1]");
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Added && e.FieldPath == "items[group=2,item=2]");
        result.Entries.Should().NotContain(e => e.FieldPath.Contains("group=1,item=1"));
    }

    [Fact]
    public void CompositeKey_StringKeys_MatchesByCompositeStringValue()
    {
        var left = CreateStruct("root", [
            CreateArray("symbols", [
                CreateStruct("sym", [CreateString("ns", "std"), CreateString("name", "vector"), CreateInteger("size", 100)]),
                CreateStruct("sym", [CreateString("ns", "std"), CreateString("name", "map"), CreateInteger("size", 200)]),
            ], diffKeys: ["ns", "name"]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("symbols", [
                CreateStruct("sym", [CreateString("ns", "std"), CreateString("name", "vector"), CreateInteger("size", 150)]),
                CreateStruct("sym", [CreateString("ns", "custom"), CreateString("name", "map"), CreateInteger("size", 300)]),
            ], diffKeys: ["ns", "name"]),
        ]);

        var result = DiffEngine.Compare(left, right);

        // std::vector size changed
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Changed && e.FieldPath == "symbols[ns=std,name=vector].size");
        // std::map removed (ns=std,name=map not in right)
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Removed && e.FieldPath == "symbols[ns=std,name=map]");
        // custom::map added
        result.Entries.Should().Contain(e => e.Kind == DiffKind.Added && e.FieldPath == "symbols[ns=custom,name=map]");
    }

    [Fact]
    public void CompositeKey_EmptyKeyList_FallsBackToIndex()
    {
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
            ], diffKeys: []),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
            ], diffKeys: []),
        ]);

        var result = DiffEngine.Compare(left, right);

        // Index-based: [0] and [1] both changed
        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().Contain(e => e.FieldPath == "items[0].id");
        result.Entries.Should().Contain(e => e.FieldPath == "items[1].id");
    }

    [Fact]
    public void KeyedArray_NoDiffKey_FallsBackToIndexComparison()
    {
        // Without diff_key, should use index-based comparison
        var left = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
            ]),
        ]);
        var right = CreateStruct("root", [
            CreateArray("items", [
                CreateStruct("entry", [CreateInteger("id", 2), CreateString("val", "B")]),
                CreateStruct("entry", [CreateInteger("id", 1), CreateString("val", "A")]),
            ]),
        ]);

        var result = DiffEngine.Compare(left, right);

        // Index-based: [0] id changed 1→2, [1] id changed 2→1
        result.HasDifferences.Should().BeTrue();
        result.Entries.Should().Contain(e => e.FieldPath == "items[0].id");
        result.Entries.Should().Contain(e => e.FieldPath == "items[1].id");
    }
}
