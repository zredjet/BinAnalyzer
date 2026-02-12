using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Diff;
using BinAnalyzer.Output;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Integration.Tests;

public class DiffOutputTests
{
    [Fact]
    public void NoDifferences_DisplaysNoDiffMessage()
    {
        var result = new DiffResult { Entries = [] };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("差分なし");
    }

    [Fact]
    public void ChangedEntry_DisplaysArrow()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Changed, "header.width", "100", "200"),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("~");
        output.Should().Contain("header.width");
        output.Should().Contain("100");
        output.Should().Contain("→");
        output.Should().Contain("200");
    }

    [Fact]
    public void AddedEntry_DisplaysPlus()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Added, "chunks[1]", null, "[struct]"),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("+");
        output.Should().Contain("chunks[1]");
    }

    [Fact]
    public void RemovedEntry_DisplaysMinus()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Removed, "chunks[2]", "[struct]", null),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("-");
        output.Should().Contain("chunks[2]");
    }

    [Fact]
    public void MultipleEntries_DisplaysCount()
    {
        var result = new DiffResult
        {
            Entries = [
                new DiffEntry(DiffKind.Changed, "width", "100", "200"),
                new DiffEntry(DiffKind.Changed, "height", "100", "150"),
                new DiffEntry(DiffKind.Added, "extra", null, "new"),
            ],
        };
        var formatter = new DiffOutputFormatter();

        var output = formatter.Format(result);

        output.Should().Contain("差分: 3 件");
    }

    // --- DiffTreeOutputFormatter tests ---

    [Fact]
    public void TreeFormat_NoDifferences_ShowsAllIdentical()
    {
        var left = MakeStruct("root", [
            MakeInteger("width", 100),
            MakeInteger("height", 200),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("width", 100),
            MakeInteger("height", 200),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("width");
        output.Should().Contain("(同一)");
        output.Should().Contain("height");
        formatter.HasDifferences.Should().BeFalse();
    }

    [Fact]
    public void TreeFormat_ChangedValue_ShowsArrow()
    {
        var left = MakeStruct("root", [
            MakeInteger("width", 100),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("width", 200),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("width");
        output.Should().Contain("100");
        output.Should().Contain("→");
        output.Should().Contain("200");
        formatter.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void TreeFormat_AddedField_ShowsPlus()
    {
        var left = MakeStruct("root", [
            MakeInteger("width", 100),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("width", 100),
            MakeInteger("height", 200),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("+ height: 200");
        formatter.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void TreeFormat_RemovedField_ShowsMinus()
    {
        var left = MakeStruct("root", [
            MakeInteger("width", 100),
            MakeInteger("height", 200),
        ]);
        var right = MakeStruct("root", [
            MakeInteger("width", 100),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("- height: 200");
        formatter.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void TreeFormat_NestedStruct_IndentsCorrectly()
    {
        var left = MakeStruct("root", [
            MakeStruct("header", [
                MakeInteger("width", 100),
                MakeInteger("height", 200),
            ]),
        ]);
        var right = MakeStruct("root", [
            MakeStruct("header", [
                MakeInteger("width", 200),
                MakeInteger("height", 200),
            ]),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("header");
        // width is changed, should be indented under header
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var headerLine = lines.First(l => l.Contains("header"));
        var widthLine = lines.First(l => l.Contains("width"));
        // width should have more leading spaces than header
        var headerIndent = headerLine.Length - headerLine.TrimStart().Length;
        var widthIndent = widthLine.Length - widthLine.TrimStart().Length;
        widthIndent.Should().BeGreaterThan(headerIndent);
    }

    [Fact]
    public void TreeFormat_ArrayDiff_ShowsElementChanges()
    {
        var left = MakeStruct("root", [
            MakeArray("items", [
                MakeInteger("item", 10),
                MakeInteger("item", 20),
                MakeInteger("item", 30),
            ]),
        ]);
        var right = MakeStruct("root", [
            MakeArray("items", [
                MakeInteger("item", 10),
                MakeInteger("item", 99),
            ]),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        // [0] should be identical
        output.Should().Contain("[0]");
        // [1] should show change 20 → 99
        output.Should().Contain("20");
        output.Should().Contain("99");
        output.Should().Contain("→");
        // [2] should be removed
        output.Should().Contain("- [2]");
        formatter.HasDifferences.Should().BeTrue();
    }

    private static DecodedStruct MakeStruct(string name, List<DecodedNode> children)
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

    private static DecodedInteger MakeInteger(string name, long value)
    {
        return new DecodedInteger { Name = name, Offset = 0, Size = 4, Value = value };
    }

    private static DecodedArray MakeArray(string name, List<DecodedNode> elements, string? diffKey = null, IReadOnlyList<string>? diffKeys = null)
    {
        var resolvedKeys = diffKeys ?? (diffKey is not null ? [diffKey] : null);
        return new DecodedArray { Name = name, Offset = 0, Size = 0, Elements = elements, DiffKey = resolvedKeys };
    }

    // --- Keyed array tree format tests ---

    [Fact]
    public void TreeFormat_KeyedArray_IdenticalElements_ShowsIdentical()
    {
        var left = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("id", 1), MakeInteger("val", 10)]),
            ], diffKey: "id"),
        ]);
        var right = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("id", 1), MakeInteger("val", 10)]),
            ], diffKey: "id"),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("items");
        output.Should().Contain("(同一)");
        formatter.HasDifferences.Should().BeFalse();
    }

    [Fact]
    public void TreeFormat_KeyedArray_ChangedElement_ShowsKeyAndArrow()
    {
        var left = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("id", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("id", 2), MakeInteger("val", 20)]),
            ], diffKey: "id"),
        ]);
        var right = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("id", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("id", 2), MakeInteger("val", 99)]),
            ], diffKey: "id"),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("[id=1]");
        output.Should().Contain("[id=2]");
        output.Should().Contain("20");
        output.Should().Contain("→");
        output.Should().Contain("99");
        formatter.HasDifferences.Should().BeTrue();
    }

    // --- Composite key tree format tests ---

    [Fact]
    public void TreeFormat_CompositeKey_ChangedElement_ShowsCompositeKeyLabel()
    {
        var left = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 2), MakeInteger("val", 20)]),
            ], diffKeys: ["group", "item"]),
        ]);
        var right = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 2), MakeInteger("val", 99)]),
            ], diffKeys: ["group", "item"]),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("[group=1,item=1]");
        output.Should().Contain("[group=1,item=2]");
        output.Should().Contain("20");
        output.Should().Contain("→");
        output.Should().Contain("99");
        formatter.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void TreeFormat_CompositeKey_AddedAndRemoved_ShowsPlusMinus()
    {
        var left = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("group", 2), MakeInteger("item", 1), MakeInteger("val", 20)]),
            ], diffKeys: ["group", "item"]),
        ]);
        var right = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("group", 2), MakeInteger("item", 2), MakeInteger("val", 30)]),
            ], diffKeys: ["group", "item"]),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("- [group=2,item=1]");
        output.Should().Contain("+ [group=2,item=2]");
        formatter.HasDifferences.Should().BeTrue();
    }

    [Fact]
    public void TreeFormat_CompositeKey_IdenticalElements_ShowsIdentical()
    {
        var left = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 1), MakeInteger("val", 10)]),
            ], diffKeys: ["group", "item"]),
        ]);
        var right = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("group", 1), MakeInteger("item", 1), MakeInteger("val", 10)]),
            ], diffKeys: ["group", "item"]),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("items");
        output.Should().Contain("(同一)");
        formatter.HasDifferences.Should().BeFalse();
    }

    [Fact]
    public void TreeFormat_KeyedArray_AddedAndRemoved_ShowsPlusMinus()
    {
        var left = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("id", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("id", 2), MakeInteger("val", 20)]),
            ], diffKey: "id"),
        ]);
        var right = MakeStruct("root", [
            MakeArray("items", [
                MakeStruct("entry", [MakeInteger("id", 1), MakeInteger("val", 10)]),
                MakeStruct("entry", [MakeInteger("id", 3), MakeInteger("val", 30)]),
            ], diffKey: "id"),
        ]);

        var formatter = new DiffTreeOutputFormatter();
        var output = formatter.Format(left, right);

        output.Should().Contain("- [id=2]");
        output.Should().Contain("+ [id=3]");
        formatter.HasDifferences.Should().BeTrue();
    }
}
