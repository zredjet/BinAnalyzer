using BinAnalyzer.Gui.Abstractions;
using BinAnalyzer.Gui.Components;
using BinAnalyzer.Gui.State;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BinAnalyzer.Gui.Tests;

/// <summary>REQ-177: ツリーは平坦な行として描画し、配列は PageSize ごとに「さらに表示」でページングする。</summary>
public sealed class StructTreeTests : BunitContext
{
    private async Task<GuiDocument> OpenPngWithChunksAsync(int idatCount)
    {
        var session = new GuiSession(FakeFormatCatalog.WithPngAndBmp(), new FakeFileSource());
        Services.AddSingleton(session);
        JSInterop.Mode = JSRuntimeMode.Loose;
        var doc = await session.OpenAsync(new OpenedFile("many.png", ManyChunkPng(idatCount)));
        return doc!;
    }

    [Fact]
    public async Task Rows_AreFlat_WithDepthIndent()
    {
        var doc = await OpenPngWithChunksAsync(1);
        doc.Select(doc.Index.ByPath("chunks[0].data.width")!);
        var cut = Render<StructTree>(p => p.Add(x => x.Document, doc));

        cut.FindAll(".tree ul").Should().BeEmpty("入れ子の ul ではなく平坦な行");
        // PNG → chunks → [0] → data → width なので深さ 4（aria-level は 1 始まり）
        var width = cut.Find(".node.sel");
        width.GetAttribute("aria-level").Should().Be("5");
        width.GetAttribute("style").Should().Contain($"padding-left:{4 + 4 * StructTreeRow.IndentPerLevel}px");
        cut.Find(".node[data-n=\"0\"]").GetAttribute("aria-level").Should().Be("1");
    }

    [Fact]
    public async Task LargeArray_ShowsPage_ThenMoreButtonExtends()
    {
        var doc = await OpenPngWithChunksAsync(StructTree.PageSize + 20);
        doc.IsExpanded(doc.Index.IdOf(doc.Index.ByPath("chunks")!)).Should().BeTrue("ルート直下は最初から展開されている");

        var cut = Render<StructTree>(p => p.Add(x => x.Document, doc));
        var more = cut.Find(".node.more");
        more.TextContent.Should().Contain($"残り {StructTree.PageSize + 22 - StructTree.PageSize}");
        cut.FindAll(".node").Count.Should().Be(3 + StructTree.PageSize + 1, "ルート + signature + chunks + 1 ページ + さらに表示");

        more.Click();
        cut.WaitForAssertion(() => cut.FindAll(".node.more").Should().BeEmpty());
    }

    [Fact]
    public async Task SelectingHiddenElement_ExpandsAncestors_AndRowAppears()
    {
        var doc = await OpenPngWithChunksAsync(3);
        doc.CollapseAll();
        var cut = Render<StructTree>(p => p.Add(x => x.Document, doc));
        cut.FindAll(".node").Should().HaveCount(3);

        var crc = doc.Index.ByPath("chunks[2].crc")!;
        doc.Select(crc);
        cut.Render();
        cut.Find(".node.sel").GetAttribute("data-n").Should().Be(doc.Index.IdOf(crc).ToString());
        cut.FindAll(".trow.collapsed .node").Should().NotBeEmpty("他のチャンクは畳まれたまま");
    }

    /// <summary>signature + IHDR + IDAT × n + IEND。CRC は検証しないので 0 のまま。</summary>
    private static byte[] ManyChunkPng(int idatCount)
    {
        var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(ms, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0]);
        for (var i = 0; i < idatCount; i++)
            WriteChunk(ms, "IDAT", [0x78, 0x9C, 0x01, 0x00]);
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = data.Length;
        s.Write([(byte)(len >> 24), (byte)(len >> 16), (byte)(len >> 8), (byte)len]);
        s.Write(System.Text.Encoding.ASCII.GetBytes(type));
        s.Write(data);
        s.Write([0, 0, 0, 0]);
    }
}
