using BinAnalyzer.Core.Models;
using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.State;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-156: 差分表示中、ヘックスで相手と異なるバイトを強調する。</summary>
public sealed class ByteDiffHighlightTests : BunitContext
{
    private async Task<(GuiSession Session, GuiDocument Left, GuiDocument Right)> CompareAsync(byte[]? rightBytes = null)
    {
        var (session, left) = await SessionFactory.WithPngAsync(width: 1, name: "v1.png");
        var right = (await session.OpenAsync(new OpenedFile("v2.png", rightBytes ?? TestPng.Bytes(width: 2))))!;
        session.Activate(left);
        session.Compare(right);
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        return (session, left, right);
    }

    [Fact]
    public async Task WithoutDiff_NothingIsMarked()
    {
        // byte[] の null が空の ReadOnlyMemory に変換されて全バイトが差分扱いになった不具合の回帰防止
        var (session, doc) = await SessionFactory.WithPngAsync();
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;

        doc.Hex.IsComparing.Should().BeFalse();
        Render<HexView>(p => p.Add(x => x.Document, doc)).FindAll(".hex .dx").Should().BeEmpty();
    }

    [Fact]
    public async Task Compare_GivesEachSideTheOthersBytes()
    {
        var (session, left, right) = await CompareAsync();

        left.CompareData.Should().BeSameAs(right.Data);
        right.CompareData.Should().BeSameAs(left.Data);
        left.Hex.IsComparing.Should().BeTrue();
        // width（4 バイト中の 1 バイト）と、それに伴う IHDR の CRC（4 バイト）
        session.Diff!.DifferentBytes.Should().BeInRange(2, 5);
    }

    [Fact]
    public async Task HexView_MarksDifferingBytes()
    {
        var (_, left, _) = await CompareAsync();

        var cut = Render<HexView>(p => p.Add(x => x.Document, left));

        var marked = cut.FindAll(".hex .b.dx");
        marked.Should().NotBeEmpty();
        marked[0].TextContent.Should().Be("01", "width の最下位バイト（v1 は 1、v2 は 2）");
        cut.FindAll(".hex .a.dx").Should().HaveCount(marked.Count, "ASCII 列も同じバイトを強調");
    }

    [Fact]
    public async Task IdenticalFiles_NoMarks()
    {
        var (session, left, _) = await CompareAsync(TestPng.Bytes(width: 1));

        session.Diff!.DifferentBytes.Should().Be(0);
        Render<HexView>(p => p.Add(x => x.Document, left)).FindAll(".hex .dx").Should().BeEmpty();
    }

    [Fact]
    public async Task DifferentLength_BytesBeyondTheShorterEnd_AreMarked()
    {
        var longer = TestPng.Bytes(width: 1).Concat(new byte[] { 0xAA, 0xBB }).ToArray();
        var (session, _, right) = await CompareAsync(longer);

        session.Diff!.DifferentBytes.Should().Be(2);
        var cut = Render<HexView>(p => p.Add(x => x.Document, right));
        cut.FindAll(".hex .b.dx").Select(e => e.TextContent).Should().Equal("AA", "BB");
    }

    [Fact]
    public async Task ClearingOrInvalidatingTheDiff_RemovesMarks()
    {
        var (session, left, right) = await CompareAsync();

        session.ClearDiff();

        left.CompareData.Should().BeNull();
        right.CompareData.Should().BeNull();
        left.Hex.IsComparing.Should().BeFalse();

        session.Compare(right);
        left.CompareData.Should().NotBeNull();
        session.ChangeEndian(left, Endianness.Little);
        left.CompareData.Should().BeNull("構造差分と一緒にバイト差分も無効になる");
        right.CompareData.Should().BeNull();
    }

    [Fact]
    public async Task Swap_KeepsBothSidesMarked()
    {
        var (session, left, right) = await CompareAsync();

        session.SwapDiff();

        session.Diff!.Left.Should().BeSameAs(right);
        left.CompareData.Should().BeSameAs(right.Data);
        right.CompareData.Should().BeSameAs(left.Data);
    }

    [Fact]
    public async Task DiffView_ShowsByteDifferenceCount()
    {
        var (session, _, _) = await CompareAsync();

        var cut = Render<DiffView>();

        cut.Find(".diffsum .bytediff").TextContent.Should().Be($"バイト差分 {FilePicker.FormatSize(session.Diff!.DifferentBytes)}");
    }
}
