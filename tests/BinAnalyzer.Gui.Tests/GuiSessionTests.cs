using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.State;
using FluentAssertions;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

public sealed class GuiSessionTests
{
    [Fact]
    public async Task Open_DetectsFormatByExtension_AndActivatesTab()
    {
        var (session, doc) = await SessionFactory.WithPngAsync();

        session.Documents.Should().ContainSingle().Which.Should().BeSameAs(doc);
        session.Active.Should().BeSameAs(doc);
        session.ShowPicker.Should().BeFalse();
        doc.Format.File.Should().Be("png.bdef.yaml");
        doc.DecodeFailure.Should().BeNull();
        doc.Root.Name.Should().Be("PNG");
        doc.Summary.ChecksumTotal.Should().Be(3);
        doc.Summary.AllChecksumsValid.Should().BeTrue();
        doc.Index.FieldCount.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task Open_UnknownExtension_BecomesPending_ThenOpensWithChosenFormat()
    {
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), new FakeFileSource());
        var changed = 0;
        session.Changed += () => changed++;

        var result = await session.OpenAsync(new OpenedFile("mystery.bin", TestPng.Bytes()));

        result.Should().BeNull();
        session.PendingFile.Should().NotBeNull();
        session.Message.Should().Contain("mystery.bin");
        changed.Should().Be(1);

        var doc = await session.OpenPendingAsync("png.bdef.yaml");
        doc.Should().NotBeNull();
        session.PendingFile.Should().BeNull();
        session.Active.Should().BeSameAs(doc);
    }

    [Fact]
    public async Task Close_ActivatesNeighbour_AndShowsPickerWhenEmpty()
    {
        var (session, a) = await SessionFactory.WithPngAsync(name: "a.png");
        var b = (await session.OpenAsync(new OpenedFile("b.png", TestPng.Bytes())))!;
        var c = (await session.OpenAsync(new OpenedFile("c.png", TestPng.Bytes())))!;
        session.Activate(b);

        session.Close(b);
        session.Active.Should().BeSameAs(c);
        session.Close(c);
        session.Active.Should().BeSameAs(a);
        session.Close(a);
        session.Active.Should().BeNull();
        session.ShowPicker.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEndian_Redecodes_AndRestoresSelectionByPath()
    {
        var (session, doc) = await SessionFactory.WithPngAsync();
        var length = doc.Index.ByPath("chunks[0].length")!;
        doc.Select(length);
        var events = 0;
        doc.Changed += () => events++;

        session.ChangeEndian(doc, Endianness.Little);

        doc.EndianOverride.Should().Be(Endianness.Little);
        events.Should().Be(1);
        doc.Selected.Should().NotBeNull();
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0].length");
        doc.Selected.Should().NotBeSameAs(length, "再デコードでツリーは作り直される");
        ((DecodedInteger)doc.Selected!).Value.Should().Be(0x0D000000, "LE で読み直されている");
        doc.Summary.Errors.Should().BeGreaterThan(0, "PNG を LE で読むと壊れる");

        // 選択先が消える場合は未選択になる
        doc.Select(doc.Index.ByPath("chunks[0].type")!);
        session.ChangeEndian(doc, null);
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0].type");
        doc.Select(doc.Index.ByPath("chunks[0].data.width")!);
        session.ChangeEndian(doc, Endianness.Little);
        doc.Selected.Should().BeNull();
    }

    [Fact]
    public async Task ChangeFormat_Redecodes_WithNewDefinition()
    {
        var (session, doc) = await SessionFactory.WithPngAsync();
        await session.ChangeFormatAsync(doc, "bmp.bdef.yaml");
        doc.Format.File.Should().Be("bmp.bdef.yaml");
        doc.Root.Should().NotBeNull();
    }

    [Fact]
    public async Task Compare_ProducesDiff_AndRedecodeInvalidatesIt()
    {
        var (session, left) = await SessionFactory.WithPngAsync(width: 1, name: "v1.png");
        var right = (await session.OpenAsync(new OpenedFile("v2.png", TestPng.Bytes(width: 2))))!;
        session.Activate(left);

        session.Compare(right);

        session.Diff.Should().NotBeNull();
        session.Pane.Should().Be(PaneKind.Diff);
        session.Diff!.Result.Entries.Select(e => e.FieldPath).Should().Contain("chunks[0].data.width");
        session.Diff.Result.Entries.Select(e => e.FieldPath).Should().Contain("chunks[0].crc");

        session.SwapDiff();
        session.Diff!.Left.Should().BeSameAs(right);

        session.ChangeEndian(left, Endianness.Little);
        session.Diff.Should().BeNull();
        session.Pane.Should().Be(PaneKind.Structure);
    }

    [Fact]
    public async Task Search_ByPattern_AndByName_SelectsFirstMatch_AndCycles()
    {
        var (_, doc) = await SessionFactory.WithPngAsync();

        doc.Search("**.width");
        doc.SearchResults.Should().HaveCount(1);
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[0].data.width");

        doc.Search("crc");
        doc.SearchResults.Should().HaveCount(3);
        doc.SearchIndex.Should().Be(0);
        doc.NextMatch();
        doc.SearchIndex.Should().Be(1);
        doc.Index.PathOf(doc.Selected!).Should().Be("chunks[1].crc");
        doc.PreviousMatch();
        doc.PreviousMatch();
        doc.SearchIndex.Should().Be(2);

        doc.Search("");
        doc.SearchResults.Should().BeEmpty();
    }

    [Fact]
    public async Task Select_ExpandsAncestors_AndHoverRaisesSeparateEvent()
    {
        var (session, doc) = await SessionFactory.WithPngAsync();
        doc.CollapseAll();
        var width = doc.Index.ByPath("chunks[0].data.width")!;
        var hovers = 0;
        session.HoverChanged += () => hovers++;

        doc.Select(width);
        for (var p = doc.Index.ParentIdOf(doc.Index.IdOf(width)); p >= 0; p = doc.Index.ParentIdOf(p))
            doc.IsExpanded(p).Should().BeTrue();

        doc.Hover(3);
        doc.Hover(3);
        hovers.Should().Be(1);
        doc.HoveredId.Should().Be(3);
    }

    [Fact]
    public async Task PickAndCompare_OpensSecondFileWithSameFormat()
    {
        var files = new FakeFileSource();
        files.Files.Enqueue(new OpenedFile("other.dat", TestPng.Bytes(width: 3)));
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), files);
        var left = (await session.OpenAsync(new OpenedFile("a.png", TestPng.Bytes())))!;

        await session.PickAndCompareAsync();

        session.Documents.Should().HaveCount(2);
        session.Documents[1].Format.File.Should().Be("png.bdef.yaml");
        session.Diff!.Left.Should().BeSameAs(left);
        session.Active.Should().BeSameAs(left);
    }
}
